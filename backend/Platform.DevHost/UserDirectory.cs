using System.Data;
using System.Security.Claims;
using Dapper;
using Npgsql;
using Primworth_API.Repository;
using Primworth_API.ViewModels;

namespace Platform.DevHost
{
    public class PermissionDto
    {
        public string permission_code { get; set; } = "";
    }

    public class AssignedYardDto
    {
        public long yard_type_id { get; set; }
        public string yard_type_name { get; set; } = "";
        public string? description { get; set; }
        public long project_id { get; set; }
        public long sub_project_id { get; set; }
    }

    public class AppUserDto
    {
        public long id { get; set; }
        public string first_name { get; set; } = "";
        public string last_name { get; set; } = "";
        public string email { get; set; } = "";
        public string? phone { get; set; }
        public bool is_admin { get; set; }
        public bool is_active { get; set; }
        public List<AssignedYardDto> yards { get; set; } = new();
    }

    public class AuthProfile
    {
        public long id { get; set; }
        public string email { get; set; } = "";
        public string first_name { get; set; } = "";
        public string last_name { get; set; } = "";
        public string role_name { get; set; } = "User";
        public bool is_admin { get; set; }
        public bool is_super_admin { get; set; }
        public List<PermissionDto> permissions { get; set; } = new();
        public List<AssignedYardDto> yards { get; set; } = new();
    }

    public class UserWrite
    {
        public string? first_name { get; set; }
        public string? last_name { get; set; }
        public string? email { get; set; }
        public string? phone { get; set; }
        public string? password { get; set; }
        public bool is_admin { get; set; }
        public bool is_active { get; set; } = true;
        public List<long> yard_type_ids { get; set; } = new();
    }

    public readonly record struct SaveResult(AppUserDto? User, string? Error, int Status)
    {
        public static SaveResult Ok(AppUserDto user) => new(user, null, StatusCodes.Status200OK);
        public static SaveResult Fail(string error, int status = StatusCodes.Status400BadRequest) => new(null, error, status);
    }

    /// <summary>
    /// Sign-in accounts in public.sys_users and the yards each account may open.
    /// The development sign-in from appsettings is created here and, while it has no yards, receives every yard.
    /// </summary>
    public class UserDirectory
    {
        public static readonly string[] RegularPermissions = { "YRDL", "YRDD", "YRDO", "YRDQ", "YRDU", "YRDA", "YRDH" };

        private readonly DapperContext _context;
        private readonly DevOptions _dev;

        public UserDirectory(DapperContext context, DevOptions dev)
        {
            _context = context;
            _dev = dev;
        }

        public async Task EnsureAsync(ILogger logger)
        {
            using var conn = _context.CreateConnection();
            conn.Open();
            await conn.ExecuteAsync("""
                ALTER TABLE public.sys_users ADD COLUMN IF NOT EXISTS password_hash text;
                ALTER TABLE public.sys_users ADD COLUMN IF NOT EXISTS phone varchar(30);
                ALTER TABLE public.sys_users ADD COLUMN IF NOT EXISTS is_admin boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE public.sys_users ADD COLUMN IF NOT EXISTS created_on timestamp NOT NULL DEFAULT NOW();
                ALTER TABLE public.sys_users ADD COLUMN IF NOT EXISTS updated_on timestamp NOT NULL DEFAULT NOW();
                CREATE UNIQUE INDEX IF NOT EXISTS uq_sys_users_email ON public.sys_users (LOWER(email));
                CREATE SEQUENCE IF NOT EXISTS public.sys_users_id_seq AS bigint;
                CREATE TABLE IF NOT EXISTS public.sys_user_yard (
                    user_id        bigint NOT NULL REFERENCES public.sys_users (id) ON DELETE CASCADE,
                    project_id     bigint NOT NULL,
                    sub_project_id bigint NOT NULL,
                    yard_type_id   bigint NOT NULL,
                    PRIMARY KEY (user_id, project_id, sub_project_id, yard_type_id)
                );
                """);

            var adminId = await EnsureBootstrapUserAsync(conn);
            await conn.ExecuteAsync(
                "SELECT setval('public.sys_users_id_seq', (SELECT COALESCE(MAX(id), 1) FROM public.sys_users), true)");
            var seededAdminId = await EnsureInitialAdminAsync(conn, logger);

            var yardAdminId = seededAdminId ?? adminId;
            var assigned = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM public.sys_user_yard WHERE user_id = @id", new { id = yardAdminId });
            if (assigned == 0)
            {
                var yards = await AllYardsAsync(conn);
                if (yards.Count > 0)
                {
                    await conn.ExecuteAsync("""
                        INSERT INTO public.sys_user_yard (user_id, project_id, sub_project_id, yard_type_id)
                        VALUES (@user_id, @project_id, @sub_project_id, @yard_type_id)
                        ON CONFLICT DO NOTHING
                        """, yards.Select(y => new
                    {
                        user_id = yardAdminId,
                        y.project_id,
                        y.sub_project_id,
                        y.yard_type_id,
                    }));
                    logger.LogInformation("[DevHost] assigned {Count} yard(s) to administrator {Id} because that account had none.",
                        yards.Count, yardAdminId);
                }
            }
            logger.LogInformation("[DevHost] users table ready.");
        }

        public async Task<(AuthProfile? profile, string? error)> AuthenticateAsync(string? email, string? password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
                return (null, "Invalid email or password.");

            using var conn = _context.CreateConnection();
            var row = await conn.QueryFirstOrDefaultAsync<UserRow>("""
                SELECT id, first_name, last_name, email, phone, password_hash, role_id, is_admin, is_active
                FROM public.sys_users
                WHERE LOWER(email) = LOWER(@email)
                """, new { email = email.Trim() });

            if (row == null || !PasswordHasher.Verify(password, row.password_hash))
                return (null, "Invalid email or password.");
            if (!row.is_active)
                return (null, "This account is inactive. Ask an administrator to activate it.");

            return (await ProfileAsync(conn, row), null);
        }

        public async Task<AuthProfile?> ProfileByIdAsync(long userId)
        {
            using var conn = _context.CreateConnection();
            var row = await LoadAsync(conn, userId);
            return row == null ? null : await ProfileAsync(conn, row);
        }

        public async Task<IResult?> DenyUnlessAdminAsync(HttpContext http)
        {
            var id = CurrentUserId(http);
            if (id <= 0)
                return Denied("Sign in required.", StatusCodes.Status401Unauthorized);

            using var conn = _context.CreateConnection();
            var admin = await conn.ExecuteScalarAsync<bool?>(
                "SELECT is_admin AND is_active FROM public.sys_users WHERE id = @id", new { id });
            if (admin == true) return null;
            return Denied("Only an administrator can manage users.", StatusCodes.Status403Forbidden);
        }

        public async Task<List<AppUserDto>> ListAsync()
        {
            using var conn = _context.CreateConnection();
            var rows = (await conn.QueryAsync<UserRow>("""
                SELECT id, first_name, last_name, email, phone, role_id, is_admin, is_active
                FROM public.sys_users
                ORDER BY LOWER(first_name), LOWER(last_name), id
                """)).ToList();
            var links = (await conn.QueryAsync<YardLink>(
                "SELECT user_id, project_id, sub_project_id, yard_type_id FROM public.sys_user_yard")).ToList();
            var catalog = await AllYardsAsync(conn);
            return rows.Select(row => ToDto(row, YardsFor(row.id, links, catalog))).ToList();
        }

        public async Task<SaveResult> CreateAsync(UserWrite input, long? projectId, long? subProjectId)
        {
            var problem = Validate(input, passwordRequired: true);
            if (problem != null) return SaveResult.Fail(problem);

            using var conn = _context.CreateConnection();
            conn.Open();
            var facility = await FacilityAsync(conn, projectId, subProjectId);
            if (facility == null) return SaveResult.Fail("Select a project before assigning yards.");
            var yards = await ResolveYardsAsync(conn, facility.Value, input.yard_type_ids);
            if (yards.Error != null) return SaveResult.Fail(yards.Error);

            var email = input.email!.Trim();
            var taken = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM public.sys_users WHERE LOWER(email) = LOWER(@email)", new { email });
            if (taken > 0) return SaveResult.Fail("A user with this email already exists.", StatusCodes.Status409Conflict);

            using var tx = conn.BeginTransaction();
            try
            {
                var id = await conn.ExecuteScalarAsync<long>("""
                    INSERT INTO public.sys_users
                        (id, first_name, last_name, email, phone, password_hash, role_id, is_admin, is_active)
                    VALUES
                        (nextval('public.sys_users_id_seq'), @first_name, @last_name, @email, @phone, @password_hash,
                         @role_id, @is_admin, @is_active)
                    RETURNING id
                    """, new
                {
                    first_name = input.first_name!.Trim(),
                    last_name = input.last_name!.Trim(),
                    email,
                    phone = BlankToNull(input.phone),
                    password_hash = PasswordHasher.Hash(input.password!),
                    role_id = input.is_admin ? 1 : 2,
                    input.is_admin,
                    input.is_active,
                }, tx);
                await ReplaceYardsAsync(conn, tx, id, facility.Value, yards.Yards);
                tx.Commit();
                var created = await LoadDtoAsync(conn, id);
                return created == null
                    ? SaveResult.Fail("The user was created but could not be read back.", StatusCodes.Status500InternalServerError)
                    : SaveResult.Ok(created);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return SaveResult.Fail("A user with this email already exists.", StatusCodes.Status409Conflict);
            }
        }

        public async Task<SaveResult> UpdateAsync(long id, UserWrite input, long? projectId, long? subProjectId, long actorId)
        {
            var problem = Validate(input, passwordRequired: false);
            if (problem != null) return SaveResult.Fail(problem);

            using var conn = _context.CreateConnection();
            conn.Open();
            var existing = await LoadAsync(conn, id);
            if (existing == null) return SaveResult.Fail("User not found.", StatusCodes.Status404NotFound);

            var facility = await FacilityAsync(conn, projectId, subProjectId);
            if (facility == null) return SaveResult.Fail("Select a project before assigning yards.");
            var yards = await ResolveYardsAsync(conn, facility.Value, input.yard_type_ids);
            if (yards.Error != null) return SaveResult.Fail(yards.Error);

            if (id == actorId && !input.is_active)
                return SaveResult.Fail("You cannot deactivate your own account.");

            var email = input.email!.Trim();
            var taken = await conn.ExecuteScalarAsync<int>("""
                SELECT COUNT(*) FROM public.sys_users
                WHERE LOWER(email) = LOWER(@email) AND id <> @id
                """, new { email, id });
            if (taken > 0) return SaveResult.Fail("A user with this email already exists.", StatusCodes.Status409Conflict);

            if (existing.is_admin && existing.is_active && (!input.is_admin || !input.is_active))
            {
                var others = await conn.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*) FROM public.sys_users
                    WHERE is_admin AND is_active AND id <> @id
                    """, new { id });
                if (others == 0)
                    return SaveResult.Fail("Keep at least one active administrator.");
            }

            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync("""
                    UPDATE public.sys_users SET
                        first_name = @first_name,
                        last_name = @last_name,
                        email = @email,
                        phone = @phone,
                        password_hash = COALESCE(@password_hash, password_hash),
                        role_id = @role_id,
                        is_admin = @is_admin,
                        is_active = @is_active,
                        updated_on = NOW()
                    WHERE id = @id
                    """, new
                {
                    id,
                    first_name = input.first_name!.Trim(),
                    last_name = input.last_name!.Trim(),
                    email,
                    phone = BlankToNull(input.phone),
                    password_hash = string.IsNullOrWhiteSpace(input.password) ? null : PasswordHasher.Hash(input.password),
                    role_id = input.is_admin ? 1 : 2,
                    input.is_admin,
                    input.is_active,
                }, tx);
                await ReplaceYardsAsync(conn, tx, id, facility.Value, yards.Yards);
                tx.Commit();
                var updated = await LoadDtoAsync(conn, id);
                return updated == null
                    ? SaveResult.Fail("User not found.", StatusCodes.Status404NotFound)
                    : SaveResult.Ok(updated);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return SaveResult.Fail("A user with this email already exists.", StatusCodes.Status409Conflict);
            }
        }

        public async Task<SaveResult> SetActiveAsync(long id, bool isActive, long actorId)
        {
            using var conn = _context.CreateConnection();
            var existing = await LoadAsync(conn, id);
            if (existing == null) return SaveResult.Fail("User not found.", StatusCodes.Status404NotFound);
            if (id == actorId && !isActive)
                return SaveResult.Fail("You cannot deactivate your own account.");
            if (existing.is_admin && existing.is_active && !isActive)
            {
                var others = await conn.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*) FROM public.sys_users
                    WHERE is_admin AND is_active AND id <> @id
                    """, new { id });
                if (others == 0)
                    return SaveResult.Fail("Keep at least one active administrator.");
            }

            await conn.ExecuteAsync(
                "UPDATE public.sys_users SET is_active = @isActive, updated_on = NOW() WHERE id = @id",
                new { id, isActive });
            var updated = await LoadDtoAsync(conn, id);
            return updated == null
                ? SaveResult.Fail("User not found.", StatusCodes.Status404NotFound)
                : SaveResult.Ok(updated);
        }

        public async Task<SaveResult> DeleteAsync(long id)
        {
            using var conn = _context.CreateConnection();
            var existing = await LoadAsync(conn, id);
            if (existing == null) return SaveResult.Fail("User not found.", StatusCodes.Status404NotFound);
            if (existing.is_admin) return SaveResult.Fail("An administrator cannot be deleted.");

            var removed = await conn.ExecuteAsync("DELETE FROM public.sys_users WHERE id = @id AND is_admin = FALSE", new { id });
            return removed == 0
                ? SaveResult.Fail("User not found.", StatusCodes.Status404NotFound)
                : SaveResult.Ok(ToDto(existing, new List<AssignedYardDto>()));
        }

        public static long CurrentUserId(HttpContext http)
        {
            var raw = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out var id) ? id : 0;
        }

        public static IResult Denied(string message, int status) =>
            Results.Json(new ServiceResponse<object?> { Success = false, Message = message, Data = null }, statusCode: status);

        public static IResult Saved(SaveResult result, string okMessage)
        {
            if (result.Error != null) return Denied(result.Error, result.Status);
            return Results.Ok(new ServiceResponse<AppUserDto> { Success = true, Message = okMessage, Data = result.User! });
        }

        private async Task<long> EnsureBootstrapUserAsync(IDbConnection conn)
        {
            var email = _dev.User.Email.Trim();
            var row = await conn.QueryFirstOrDefaultAsync<UserRow>("""
                SELECT id, first_name, last_name, email, phone, password_hash, role_id, is_admin, is_active
                FROM public.sys_users
                WHERE LOWER(email) = LOWER(@email) OR id = @id
                ORDER BY CASE WHEN LOWER(email) = LOWER(@email) THEN 0 ELSE 1 END
                LIMIT 1
                """, new { email, id = _dev.User.Id });

            if (row == null)
            {
                await conn.ExecuteAsync("""
                    INSERT INTO public.sys_users
                        (id, first_name, last_name, email, role_id, is_active, is_admin, password_hash)
                    VALUES
                        (@id, @first_name, @last_name, @email, 1, TRUE, TRUE, @password_hash)
                    """, new
                {
                    id = _dev.User.Id,
                    first_name = _dev.User.FirstName,
                    last_name = _dev.User.LastName,
                    email,
                    password_hash = PasswordHasher.Hash(_dev.User.Password),
                });
                return _dev.User.Id;
            }

            if (string.IsNullOrEmpty(row.password_hash))
            {
                await conn.ExecuteAsync("""
                    UPDATE public.sys_users
                    SET password_hash = @password_hash, is_admin = TRUE, role_id = 1, is_active = TRUE, updated_on = NOW()
                    WHERE id = @id
                    """, new { id = row.id, password_hash = PasswordHasher.Hash(_dev.User.Password) });
            }
            return row.id;
        }

        /// <summary>
        /// Creates admin@local / 123 when that account is missing, or promotes it when the
        /// database has no administrator. Returns the id only when the row was inserted.
        /// </summary>
        private async Task<long?> EnsureInitialAdminAsync(IDbConnection conn, ILogger logger)
        {
            const string email = "admin@local";
            var existing = await conn.QueryFirstOrDefaultAsync<long?>(
                "SELECT id FROM public.sys_users WHERE LOWER(email) = LOWER(@email)", new { email });
            var admins = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.sys_users WHERE is_admin = TRUE");

            if (existing is > 0)
            {
                if (admins > 0) return null;
                await conn.ExecuteAsync("""
                    UPDATE public.sys_users
                    SET is_admin = TRUE, role_id = 1, is_active = TRUE,
                        password_hash = COALESCE(password_hash, @password_hash),
                        updated_on = NOW()
                    WHERE id = @id
                    """, new { id = existing, password_hash = PasswordHasher.Hash("123") });
                logger.LogInformation("[DevHost] marked {Email} as the initial administrator.", email);
                return existing;
            }

            var id = await conn.ExecuteScalarAsync<long>("""
                INSERT INTO public.sys_users
                    (id, first_name, last_name, email, role_id, is_active, is_admin, password_hash)
                VALUES
                    (nextval('public.sys_users_id_seq'), 'Admin', 'User', @email, 1, TRUE, TRUE, @password_hash)
                RETURNING id
                """, new { email, password_hash = PasswordHasher.Hash("123") });
            logger.LogInformation("[DevHost] created initial administrator {Email}.", email);
            return id;
        }

        private async Task<AuthProfile> ProfileAsync(IDbConnection conn, UserRow row)
        {
            var links = (await conn.QueryAsync<YardLink>("""
                SELECT user_id, project_id, sub_project_id, yard_type_id
                FROM public.sys_user_yard WHERE user_id = @id
                """, new { id = row.id })).ToList();
            var catalog = await AllYardsAsync(conn);
            var codes = row.is_admin
                ? RegularPermissions.Append("YRDM")
                : RegularPermissions;
            return new AuthProfile
            {
                id = row.id,
                email = row.email,
                first_name = row.first_name,
                last_name = row.last_name,
                role_name = row.is_admin ? "Admin" : "User",
                is_admin = row.is_admin,
                is_super_admin = row.is_admin,
                permissions = codes.Select(c => new PermissionDto { permission_code = c }).ToList(),
                yards = YardsFor(row.id, links, catalog),
            };
        }

        private static List<AssignedYardDto> YardsFor(long userId, IEnumerable<YardLink> links, IReadOnlyList<AssignedYardDto> catalog)
        {
            var wanted = links.Where(l => l.user_id == userId)
                .Select(l => (l.project_id, l.sub_project_id, l.yard_type_id))
                .ToHashSet();
            return catalog.Where(y => wanted.Contains((y.project_id, y.sub_project_id, y.yard_type_id))).ToList();
        }

        private async Task<List<AssignedYardDto>> AllYardsAsync(IDbConnection conn)
        {
            var result = new List<AssignedYardDto>();
            var projects = (await conn.QueryAsync<(long project_id, string project_name)>(
                "SELECT project_id, project_name FROM public.sys_pr_project ORDER BY project_id")).ToList();

            foreach (var project in projects)
            {
                var projectSchema = DevSchemaNames.Project(project.project_id, project.project_name);
                List<(long sub_project_id, string sub_project_name)> subs;
                try
                {
                    subs = (await conn.QueryAsync<(long sub_project_id, string sub_project_name)>(
                        $"""
                        SELECT sub_project_id, sub_project_name
                        FROM {Quote(projectSchema)}.sys_sp_sub_project
                        ORDER BY sub_project_id
                        """)).ToList();
                }
                catch (PostgresException)
                {
                    continue;
                }

                foreach (var sub in subs)
                {
                    var schema = DevSchemaNames.SubProject(project.project_id, project.project_name, sub.sub_project_id, sub.sub_project_name);
                    var exists = await conn.ExecuteScalarAsync<int>("""
                        SELECT COUNT(*) FROM information_schema.tables
                        WHERE table_schema = @schema AND table_name = 'sp_yard_type'
                        """, new { schema });
                    if (exists == 0) continue;

                    var yards = await conn.QueryAsync<(long yard_type_id, string yard_type_name, string? description)>(
                        $"""
                        SELECT yard_type_id, yard_type_name, description
                        FROM {Quote(schema)}.sp_yard_type
                        WHERE is_deleted = FALSE
                        ORDER BY yard_type_id
                        """);
                    foreach (var yard in yards)
                    {
                        result.Add(new AssignedYardDto
                        {
                            yard_type_id = yard.yard_type_id,
                            yard_type_name = yard.yard_type_name,
                            description = yard.description,
                            project_id = project.project_id,
                            sub_project_id = sub.sub_project_id,
                        });
                    }
                }
            }
            return result;
        }

        private async Task<(long projectId, long subProjectId, string schema)?> FacilityAsync(
            IDbConnection conn, long? projectId, long? subProjectId)
        {
            var project = await conn.QueryFirstOrDefaultAsync<(long project_id, string project_name)>(
                projectId is > 0
                    ? "SELECT project_id, project_name FROM public.sys_pr_project WHERE project_id = @projectId"
                    : "SELECT project_id, project_name FROM public.sys_pr_project ORDER BY project_id LIMIT 1",
                new { projectId });
            if (project.project_name == null) return null;

            var projectSchema = DevSchemaNames.Project(project.project_id, project.project_name);
            var sub = await conn.QueryFirstOrDefaultAsync<(long sub_project_id, string sub_project_name)>(
                subProjectId is > 0
                    ? $"""
                      SELECT sub_project_id, sub_project_name
                      FROM {Quote(projectSchema)}.sys_sp_sub_project
                      WHERE sub_project_id = @subProjectId
                      """
                    : $"""
                      SELECT sub_project_id, sub_project_name
                      FROM {Quote(projectSchema)}.sys_sp_sub_project
                      ORDER BY sub_project_id LIMIT 1
                      """,
                new { subProjectId });
            if (sub.sub_project_name == null) return null;

            var schema = DevSchemaNames.SubProject(project.project_id, project.project_name, sub.sub_project_id, sub.sub_project_name);
            return (project.project_id, sub.sub_project_id, schema);
        }

        private async Task<(List<AssignedYardDto> Yards, string? Error)> ResolveYardsAsync(
            IDbConnection conn, (long projectId, long subProjectId, string schema) facility, List<long> ids)
        {
            var distinct = ids.Where(id => id > 0).Distinct().ToList();
            if (distinct.Count == 0) return (new List<AssignedYardDto>(), "Choose at least one yard.");

            var known = (await conn.QueryAsync<(long yard_type_id, string yard_type_name, string? description)>(
                $"""
                SELECT yard_type_id, yard_type_name, description
                FROM {Quote(facility.schema)}.sp_yard_type
                WHERE is_deleted = FALSE AND yard_type_id = ANY(@ids)
                """, new { ids = distinct.ToArray() })).ToList();
            if (known.Count != distinct.Count)
                return (new List<AssignedYardDto>(), "One of the selected yards is no longer available.");

            return (known.Select(y => new AssignedYardDto
            {
                yard_type_id = y.yard_type_id,
                yard_type_name = y.yard_type_name,
                description = y.description,
                project_id = facility.projectId,
                sub_project_id = facility.subProjectId,
            }).ToList(), null);
        }

        private static async Task ReplaceYardsAsync(
            IDbConnection conn, IDbTransaction tx, long userId,
            (long projectId, long subProjectId, string schema) facility, List<AssignedYardDto> yards)
        {
            await conn.ExecuteAsync("""
                DELETE FROM public.sys_user_yard
                WHERE user_id = @userId AND project_id = @projectId AND sub_project_id = @subProjectId
                """, new { userId, projectId = facility.projectId, subProjectId = facility.subProjectId }, tx);
            await conn.ExecuteAsync("""
                INSERT INTO public.sys_user_yard (user_id, project_id, sub_project_id, yard_type_id)
                VALUES (@user_id, @project_id, @sub_project_id, @yard_type_id)
                """, yards.Select(y => new
            {
                user_id = userId,
                y.project_id,
                y.sub_project_id,
                y.yard_type_id,
            }), tx);
        }

        private static async Task<UserRow?> LoadAsync(IDbConnection conn, long id) =>
            await conn.QueryFirstOrDefaultAsync<UserRow>("""
                SELECT id, first_name, last_name, email, phone, role_id, is_admin, is_active
                FROM public.sys_users WHERE id = @id
                """, new { id });

        private async Task<AppUserDto?> LoadDtoAsync(IDbConnection conn, long id)
        {
            var row = await LoadAsync(conn, id);
            if (row == null) return null;
            var links = (await conn.QueryAsync<YardLink>("""
                SELECT user_id, project_id, sub_project_id, yard_type_id
                FROM public.sys_user_yard WHERE user_id = @id
                """, new { id })).ToList();
            return ToDto(row, YardsFor(id, links, await AllYardsAsync(conn)));
        }

        private static AppUserDto ToDto(UserRow row, List<AssignedYardDto> yards) => new()
        {
            id = row.id,
            first_name = row.first_name,
            last_name = row.last_name,
            email = row.email,
            phone = row.phone,
            is_admin = row.is_admin,
            is_active = row.is_active,
            yards = yards,
        };

        private static string? Validate(UserWrite input, bool passwordRequired)
        {
            if (string.IsNullOrWhiteSpace(input.first_name) || input.first_name.Trim().Length > 80)
                return "A first name is required (max 80 characters).";
            if (string.IsNullOrWhiteSpace(input.last_name) || input.last_name.Trim().Length > 80)
                return "A last name is required (max 80 characters).";
            var email = input.email?.Trim() ?? "";
            if (email.Length == 0 || email.Length > 200 || email.Contains(' ') || !email.Contains('@'))
                return "A valid email is required.";
            var phone = input.phone?.Trim() ?? "";
            if (phone.Length > 30) return "Phone must be 30 characters or fewer.";
            var password = input.password ?? "";
            if (passwordRequired && string.IsNullOrWhiteSpace(password))
                return "A password is required.";
            if (!string.IsNullOrWhiteSpace(password) && password.Length < 3)
                return "Password must be at least 3 characters.";
            return null;
        }

        private static string? BlankToNull(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        private static string Quote(string identifier)
        {
            if (identifier.Length == 0 || identifier.Any(c => c == '"' || char.IsWhiteSpace(c)))
                throw new InvalidOperationException("Unexpected schema name.");
            return "\"" + identifier + "\"";
        }

        private sealed class UserRow
        {
            public long id { get; set; }
            public string first_name { get; set; } = "";
            public string last_name { get; set; } = "";
            public string email { get; set; } = "";
            public string? phone { get; set; }
            public string? password_hash { get; set; }
            public int role_id { get; set; }
            public bool is_admin { get; set; }
            public bool is_active { get; set; }
        }

        private sealed class YardLink
        {
            public long user_id { get; set; }
            public long project_id { get; set; }
            public long sub_project_id { get; set; }
            public long yard_type_id { get; set; }
        }
    }
}
