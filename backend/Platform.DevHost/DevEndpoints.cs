using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Primworth_API.IRepository;
using Primworth_API.Repository;
using Primworth_API.ViewModels;

namespace Platform.DevHost
{
    public record LoginRequest(string email, string password);

    /// <summary>
    /// The platform endpoints an application's UI needs during development, at the platform's
    /// paths with the platform's envelope: login, branding (for the login page, sidebar and
    /// header), a health check, the project list for the shell, and a re-provision trigger.
    /// </summary>
    public static class DevEndpoints
    {
        public static void Map(WebApplication app)
        {
            app.MapGet("/api/ping", () => Results.Ok(new { ok = true, utc = DateTime.UtcNow })).AllowAnonymous();

            // Same path and answer shape as the platform. The platform puts the JWT in
            // data.password (historical); the dev host sends it in both fields.
            app.MapPost("/api/User/Login", async (LoginRequest request, UserDirectory users, DevOptions dev) =>
            {
                var (profile, error) = await users.AuthenticateAsync(request.email, request.password);
                if (profile == null)
                    return Results.Json(new ServiceResponse<object?> { Success = false, Message = error ?? "Invalid email or password.", Data = null },
                        statusCode: StatusCodes.Status401Unauthorized);

                var token = CreateToken(dev, profile);
                var data = new
                {
                    token,
                    password = token,
                    profile.id,
                    profile.email,
                    profile.first_name,
                    profile.last_name,
                    profile.role_name,
                    profile.is_admin,
                    profile.is_super_admin,
                    profile.permissions,
                    profile.yards,
                };
                return Results.Ok(new ServiceResponse<object> { Success = true, Message = "Login successful.", Data = data });
            }).AllowAnonymous();

            app.MapGet("/api/User/me", async (HttpContext http, UserDirectory users) =>
            {
                var profile = await users.ProfileByIdAsync(UserDirectory.CurrentUserId(http));
                if (profile == null)
                    return UserDirectory.Denied("Sign in required.", StatusCodes.Status401Unauthorized);
                return Results.Ok(new ServiceResponse<AuthProfile> { Success = true, Message = "Profile fetched.", Data = profile });
            });

            app.MapGet("/api/users", async (HttpContext http, UserDirectory users) =>
            {
                var deny = await users.DenyUnlessAdminAsync(http);
                if (deny != null) return deny;
                var rows = await users.ListAsync();
                return Results.Ok(new ServiceResponse<List<AppUserDto>> { Success = true, Message = "Users fetched.", Data = rows });
            });

            app.MapPost("/api/users", async (HttpContext http, UserWrite input, UserDirectory users) =>
            {
                var deny = await users.DenyUnlessAdminAsync(http);
                if (deny != null) return deny;
                var (projectId, subProjectId) = Tenant(http);
                var result = await users.CreateAsync(input, projectId, subProjectId);
                return UserDirectory.Saved(result, $"{input.first_name?.Trim()} {input.last_name?.Trim()} added.".Trim());
            });

            app.MapPut("/api/users/{id:long}", async (long id, HttpContext http, UserWrite input, UserDirectory users) =>
            {
                var deny = await users.DenyUnlessAdminAsync(http);
                if (deny != null) return deny;
                var (projectId, subProjectId) = Tenant(http);
                var result = await users.UpdateAsync(id, input, projectId, subProjectId, UserDirectory.CurrentUserId(http));
                return UserDirectory.Saved(result, "User saved.");
            });

            app.MapDelete("/api/users/{id:long}", async (long id, HttpContext http, UserDirectory users) =>
            {
                var deny = await users.DenyUnlessAdminAsync(http);
                if (deny != null) return deny;
                var result = await users.DeleteAsync(id);
                return UserDirectory.Saved(result, "User deleted.");
            });

            app.MapPut("/api/users/{id:long}/active", async (long id, HttpContext http, ActiveInput input, UserDirectory users) =>
            {
                var deny = await users.DenyUnlessAdminAsync(http);
                if (deny != null) return deny;
                var result = await users.SetActiveAsync(id, input.is_active, UserDirectory.CurrentUserId(http));
                return UserDirectory.Saved(result, input.is_active ? "User activated." : "User deactivated.");
            });

            // Branding + masters the frame reads. Anonymous in the platform too (the login page needs it).
            app.MapGet("/api/Project/GetOemAndFacilityTypes", async (DapperContext ctx, DevOptions dev) =>
            {
                using var connection = ctx.CreateConnection();
                var first = await connection.QueryFirstOrDefaultAsync<(long project_id, string project_name)>(
                    "SELECT project_id, project_name FROM public.sys_pr_project ORDER BY project_id LIMIT 1");
                IEnumerable<object> facilityTypes = Array.Empty<object>();
                if (first.project_name != null)
                {
                    var schema = DevSchemaNames.Project(first.project_id, first.project_name);
                    facilityTypes = (await connection.QueryAsync<(long facility_type_id, string facility_type_name, string? description)>(
                        $"SELECT facility_type_id, facility_type_name, description FROM \"{schema}\".gm_facility_type ORDER BY facility_type_id"))
                        .Select(f => new { f.facility_type_id, f.facility_type_name, f.description }).ToList();
                }
                var data = new
                {
                    platforms = new[] { new { id = 1, name = dev.Platform.Name, logo = dev.Platform.Logo, powered_by = dev.Platform.PoweredBy } },
                    oems = new[] { new { oem_id = 1, oem_name = dev.Platform.OemName } },
                    facility_types = facilityTypes,
                };
                return Results.Ok(new ServiceResponse<object> { Success = true, Message = "Fetched.", Data = data });
            }).AllowAnonymous();

            app.MapGet("/api/dev/projects", async (DapperContext ctx, DevOptions dev, IEnumerable<IApplicationProvisioner> provisioners) =>
            {
                using var connection = ctx.CreateConnection();
                var appNames = string.Join(",", provisioners.Select(p => p.ProvisionerKey));
                var projects = (await connection.QueryAsync<(long project_id, string project_name)>(
                    "SELECT project_id, project_name FROM public.sys_pr_project ORDER BY project_id")).ToList();
                var result = new List<object>();
                foreach (var (pid, pname) in projects)
                {
                    var schema = DevSchemaNames.Project(pid, pname);
                    var subs = (await connection.QueryAsync<(long sub_project_id, string sub_project_name, long? facility_type_id, string? facility_type_name)>(
                        $"""
                        SELECT s.sub_project_id, s.sub_project_name, s.facility_type_id, f.facility_type_name
                        FROM "{schema}".sys_sp_sub_project s
                        LEFT JOIN "{schema}".gm_facility_type f ON f.facility_type_id = s.facility_type_id
                        ORDER BY s.sub_project_id
                        """)).ToList();
                    result.Add(new
                    {
                        project_id = pid,
                        project_name = pname,
                        schema,
                        customer_name = dev.Platform.CustomerName,
                        oem_name = dev.Platform.OemName,
                        application_name = appNames,
                        sub_projects = subs.Select(s => new
                        {
                            s.sub_project_id,
                            s.sub_project_name,
                            s.facility_type_id,
                            s.facility_type_name,
                            schema = DevSchemaNames.SubProject(pid, pname, s.sub_project_id, s.sub_project_name),
                        }).ToList(),
                    });
                }
                return Results.Ok(new ServiceResponse<object> { Success = true, Message = "Projects fetched.", Data = result });
            });

            app.MapPost("/api/dev/provision", async (DevProvisioningService svc, ILogger<DevProvisioningService> logger) =>
            {
                var results = await svc.RunAllAsync(logger);
                return Results.Ok(new ServiceResponse<object> { Success = true, Message = "Provisioning run complete.", Data = results });
            });
        }

        private static (long? projectId, long? subProjectId) Tenant(HttpContext http)
        {
            static long? Read(HttpContext ctx, string name)
            {
                var raw = ctx.Request.Headers[name].ToString();
                return long.TryParse(raw, out var id) && id > 0 ? id : null;
            }
            return (Read(http, "X-Project-Id"), Read(http, "X-Sub-Project-Id"));
        }

        private static string CreateToken(DevOptions dev, AuthProfile profile)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, profile.id.ToString()),
                new(ClaimTypes.Name, profile.first_name),
                new(ClaimTypes.Email, profile.email),
                new(ClaimTypes.Role, profile.is_admin ? "1" : "2"),
                new("is_admin", profile.is_admin ? "true" : "false"),
            };
            if (profile.is_admin) claims.Add(new Claim("permission", "*"));
            else claims.AddRange(UserDirectory.RegularPermissions.Select(code => new Claim("permission", code)));

            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(dev.JwtKey));
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public record ActiveInput(bool is_active);
    }
}
