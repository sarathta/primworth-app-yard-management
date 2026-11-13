namespace Primworth_API.Queries.Yard
{
    public static class YardRuleQueries
    {
        public const string GetGraph = @"
            SELECT graph::text
            FROM ""{schema}"".sp_rules
            WHERE yard_type_id = @yard_type_id AND is_deleted = FALSE;";

        public const string YardTypeExists = @"
            SELECT COUNT(*) FROM ""{schema}"".sp_yard_type
            WHERE yard_type_id = @yard_type_id AND is_deleted = FALSE;";

        public const string UpsertGraph = @"
            INSERT INTO ""{schema}"".sp_rules (yard_type_id, graph, created_by, created_on, updated_by, updated_on)
            VALUES (@yard_type_id, @graph::jsonb, @user_id, NOW(), @user_id, NOW())
            ON CONFLICT (yard_type_id)
            DO UPDATE SET graph = EXCLUDED.graph, is_deleted = FALSE,
                          updated_by = EXCLUDED.updated_by, updated_on = NOW();";
    }
}
