using Dapper;
using Primworth_API.IRepository;
using Primworth_API.Queries.Yard;

namespace Primworth_API.Repository.Yard
{
    /// <summary>
    /// Creates Yard Management's tables inside a facility (sub-project) schema when the application
    /// is mapped there, and again, harmlessly, on every later edit.
    ///
    /// Idempotency is the contract: CREATE ... IF NOT EXISTS and INSERT ... WHERE NOT EXISTS only.
    /// There is no drop path; the platform never removes tenant tables on de-selection. An admin
    /// who wants a table gone drops it from the Masters UI, which the prefix rule below permits.
    ///
    /// The seed makes a slab plant work with no configuration: one SLAB material type carrying the
    /// slab's words and fields. A second material is a second seed row, or a row typed into the
    /// Masters UI; nothing here changes.
    /// </summary>
    public class YardProvisioner : IApplicationProvisioner
    {
        /// <summary>Must equal public.application.provisioner_key for "Yard Management".</summary>
        public const string Key = "yard_management";

        public string ProvisionerKey => Key;

        /// <summary>A yard belongs to a facility.</summary>
        public ApplicationScopeTarget Scope => ApplicationScopeTarget.SubProject;

        /// <summary>
        /// Shipped material types. Attribute definitions are the one spelling of each field, so
        /// routing rules and reports can rely on the keys. Units are part of the key's meaning.
        /// </summary>
        private static readonly (string Key, string Singular, string Plural, string IdLabel, string PositionLabel, string Defs, string RuleSet)[] MaterialTypes =
        {
            ("SLAB", "Slab", "Slabs", "Slab ID", "Pit R{row}-C{col}",
             """
             [
               {"key":"length_mm","label":"Length","type":"number","unit":"mm","required":false},
               {"key":"width_mm","label":"Width","type":"number","unit":"mm","required":false},
               {"key":"thickness_mm","label":"Thickness","type":"number","unit":"mm","required":false},
               {"key":"cast_time","label":"Cast time","type":"datetime","required":false},
               {"key":"rolling_width_mm","label":"Rolling width","type":"number","unit":"mm","required":false},
               {"key":"target_reheat_c","label":"Reheat target","type":"integer","unit":"C","required":false}
             ]
             """,
             "slab-routing"),
            // Add the next material here, or type it into the Masters UI on a live facility:
            // ("TMT_BUNDLE", "Bundle", "Bundles", "Bundle number", "Rack {row}, slot {col}", "[...]", "tmt_bundle-routing"),
        };

        /// <summary>Yards offered after login. Skipped when a row with the same name already exists.</summary>
        private static readonly (string Name, string Description)[] YardTypes =
        {
            ("Coil Yard", "Storage yard for coils"),
            ("Billet Yard", "Storage yard for billets"),
            ("TMT Storage Yard", "Storage yard for TMT material"),
            ("Slab Yard", "Storage yard for slabs"),
            ("Wire Rod Yard", "Storage yard for wire rods"),
        };

        /// <summary>
        /// Feature flags and tuning, the Rule Book the Configurator edits. Seeded once, then owned
        /// by the customer, so re-provisioning never resets a live yard. The MODULE_* names are
        /// the ones the mSYMS screens read.
        /// </summary>
        private static readonly (string Key, string Value)[] DefaultConfig =
        {
            ("rule_grade_separation",   "false"),
            ("max_stack_height",        "5"),
            ("MODULE_MASTER_ROUTING",   "false"),
            ("MODULE_QA_WORKFLOW",      "false"),
            ("MODULE_REWORK",           "false"),
            ("MODULE_THERMAL_TRACKING", "false"),
            ("MODULE_ZONE_BUILDER",     "true"),
            ("thermal_hot_minutes",     "2"),
            ("thermal_warm_minutes",    "4"),
            ("rule_nomenclature_format","R{row} - C{col}"),
            // A tag hub value shown on the yard dashboard, chosen per facility. Blank until named.
            ("dashboard_tag_path",      ""),
            ("dashboard_tag_label",     ""),
        };

        private static readonly (string Table, string DisplayName, string Description)[] TableComments =
        {
            ("sp_yard_material_type",      "Yard - Material Types",    "What the yard stores: words, fields and rule set per material."),
            ("sp_yard_type",               "Yard - Yards",             "Yards the operator opens, such as Slab Yard and Coil Yard."),
            ("sp_yard_zone",               "Yard - Areas",             "Yard areas with purpose, storage style and accepted materials."),
            ("sp_yard_zone_material_type", "Yard - Area Materials",    "Which material types each area accepts."),
            ("sp_yard_position",           "Yard - Positions",         "Addressable places inside an area, with capacity."),
            ("sp_yard_item",               "Yard - Items",             "Every item in the yard with its location, state and attributes."),
            ("sp_yard_work_order",         "Yard - Work Orders",       "Inbound, move, digging, rework, dispatch and audit jobs for the crane."),
            ("sp_yard_movement_log",       "Yard - Movement Log",      "Every physical movement, with a copy of from and to."),
            ("sp_yard_system_config",      "Yard - Configuration",     "Feature flags and tuning values."),
            ("sp_yard_size_profile",       "Yard - Size Profiles",     "Standard sizes with tolerance, used by product-mix routing."),
            ("sp_yard_rework_activity",    "Yard - Rework Activities", "Scarfing, grinding, descaling and similar activities."),
            ("sp_yard_product_mix",        "Yard - Product Mix",       "Grade and size groupings that drive routing."),
            ("sp_yard_product_mix_grade",  "Yard - Product Mix Grades","Grades allowed by a product mix."),
            ("sp_yard_product_mix_size",   "Yard - Product Mix Sizes", "Size profiles allowed by a product mix."),
            ("sp_yard_system_log",         "Yard - Activity Log",      "User and system actions for audit."),
        };

        public async Task ProvisionAsync(ApplicationProvisionContext ctx)
        {
            var schema = ctx.SchemaName;
            var userId = ctx.UserId == 0 ? (long?)null : ctx.UserId;

            // Parents first: every statement below carries foreign keys to the ones above it.
            await ExecuteAsync(ctx, YardDdl.CreateMaterialType, schema);
            await ExecuteAsync(ctx, YardDdl.CreateYardType, schema);
            await ExecuteAsync(ctx, YardDdl.CreateZone, schema);
            await ExecuteAsync(ctx, YardDdl.CreateZoneMaterialType, schema);
            await ExecuteAsync(ctx, YardDdl.CreatePosition, schema);
            await ExecuteAsync(ctx, YardDdl.CreateSizeProfile, schema);
            await ExecuteAsync(ctx, YardDdl.CreateReworkActivity, schema);
            await ExecuteAsync(ctx, YardDdl.CreateProductMix, schema);
            await ExecuteAsync(ctx, YardDdl.CreateItem, schema);
            await ExecuteAsync(ctx, YardDdl.CreateWorkOrder, schema);
            await ExecuteAsync(ctx, YardDdl.CreateMovementLog, schema);
            await ExecuteAsync(ctx, YardDdl.CreateSystemLog, schema);
            await ExecuteAsync(ctx, YardDdl.CreateSystemConfig, schema);
            await ExecuteAsync(ctx, YardDdl.AddLaterColumns, schema);

            foreach (var mt in MaterialTypes)
            {
                await ExecuteAsync(ctx, YardDdl.SeedMaterialType, schema, new
                {
                    type_key = mt.Key, singular = mt.Singular, plural = mt.Plural, id_label = mt.IdLabel,
                    position_label = mt.PositionLabel, attribute_defs = mt.Defs, rule_set_key = mt.RuleSet, user_id = userId,
                });
            }

            foreach (var (name, description) in YardTypes)
                await ExecuteAsync(ctx, YardDdl.SeedYardType, schema, new { yard_type_name = name, description });

            foreach (var (key, value) in DefaultConfig)
                await ExecuteAsync(ctx, YardDdl.SeedConfig, schema, new { config_key = key, config_value = value, user_id = userId });

            await ApplyTableCommentsAsync(ctx, schema);
        }

        /// <summary>
        /// How the tables behave in the facility's Masters UI. Order matters: the first matching
        /// rule wins, so the named tables precede the prefix catch-all.
        ///
        /// Reference data the customer maintains (material types, size profiles, rework activities)
        /// is editable, so the platform's dynamic CRUD screens can manage it. Everything else is
        /// operational state owned by the module's own screens: viewable for support, not
        /// hand-edited, droppable so an admin can force-reset one and let provisioning recreate it.
        /// </summary>
        public IReadOnlyList<SchemaTableConfig> DescribeMastersUiRules() => new[]
        {
            Editable("sp_yard_material_type"),
            Editable("sp_yard_size_profile"),
            Editable("sp_yard_rework_activity"),
            new SchemaTableConfig { to = "sub_project", type = "prefix", to_copy = "sp_yard_", editable = false, open_table = true, hide = false, droppable = true },
        };

        private static SchemaTableConfig Editable(string table) => new()
        {
            to = "sub_project", type = "tablename", to_copy = table, editable = true, open_table = true, hide = false, droppable = false,
        };

        private static Task<int> ExecuteAsync(ApplicationProvisionContext ctx, string sql, string schema, object? param = null)
            => ctx.Connection.ExecuteAsync(new CommandDefinition(sql.Replace("{schema}", schema), param, ctx.Transaction));

        private static async Task ApplyTableCommentsAsync(ApplicationProvisionContext ctx, string schema)
        {
            foreach (var (table, name, description) in TableComments)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new { n = name, d = description });
                var sql = YardDdl.CommentOnTable
                    .Replace("{schema}", schema).Replace("{table}", table).Replace("{comment}", json.Replace("'", "''"));
                await ctx.Connection.ExecuteAsync(new CommandDefinition(sql, transaction: ctx.Transaction));
            }
        }
    }
}
