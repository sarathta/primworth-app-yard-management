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
            app.MapPost("/api/User/Login", (LoginRequest request, DevOptions dev) =>
            {
                var ok = string.Equals(request.email?.Trim(), dev.User.Email, StringComparison.OrdinalIgnoreCase)
                         && request.password == dev.User.Password;
                if (!ok)
                    return Results.Json(new ServiceResponse<object?> { Success = false, Message = "Invalid email or password.", Data = null },
                        statusCode: StatusCodes.Status401Unauthorized);

                var token = CreateToken(dev);
                var permissions = dev.Permissions.Select(c => new { permission_code = c }).ToList();
                var data = new
                {
                    token,
                    password = token,
                    id = dev.User.Id,
                    email = dev.User.Email,
                    first_name = dev.User.FirstName,
                    last_name = dev.User.LastName,
                    role_name = dev.Permissions.Contains("*") ? "Dev Super Admin" : "Dev User",
                    is_super_admin = dev.Permissions.Contains("*"),
                    permissions,
                };
                return Results.Ok(new ServiceResponse<object> { Success = true, Message = "Login successful.", Data = data });
            }).AllowAnonymous();

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

        private static string CreateToken(DevOptions dev)
        {
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(dev.JwtKey));
            var token = new JwtSecurityToken(
                claims: new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, dev.User.Id.ToString()),
                    new Claim(ClaimTypes.Name, dev.User.FirstName),
                    new Claim(ClaimTypes.Email, dev.User.Email),
                    new Claim(ClaimTypes.Role, "1"),
                },
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
