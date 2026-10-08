namespace Primworth_API.Repository
{
    /// <summary>Project or sub-project — the two kinds of tenant schema.</summary>
    public enum SchemaType
    {
        Project,
        SubProject
    }

    /// <summary>
    /// A rule for how one table (or every table with a prefix) behaves in the platform's Masters
    /// screens. Returned by <c>IApplicationProvisioner.DescribeMastersUiRules</c>.
    /// </summary>
    public class SchemaTableConfig
    {
        /// <summary>'project' or 'sub_project'.</summary>
        public string to { get; set; } = "";

        /// <summary>'prefix' or 'tablename'.</summary>
        public string type { get; set; } = "";

        /// <summary>The prefix (e.g. 'rmp_') or the table name the rule applies to.</summary>
        public string to_copy { get; set; } = "";

        /// <summary>false → edit / truncate / insert / import / export disabled.</summary>
        public bool editable { get; set; }

        /// <summary>false → the Open Table button is disabled.</summary>
        public bool open_table { get; set; }

        /// <summary>true → hidden from the master list.</summary>
        public bool hide { get; set; }

        /// <summary>true → an admin may drop the table from the Masters UI.</summary>
        public bool? droppable { get; set; }
    }
}
