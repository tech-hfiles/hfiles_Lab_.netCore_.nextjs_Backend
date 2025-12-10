using System.Text.Json;
using HFiles_Backend.Application.DTOs.Labs;

namespace HFiles_Backend.Application.Common
{
    public static class NotificationMessageRegistry
    {
        private static readonly Dictionary<string, Func<object?, string>> RouteMap = new()
        {
            // Promote Admin API
            ["/api/labs/admin/promote"] = dto =>
            {
                try
                {
                    if (dto is string raw && raw.Contains("NewSuperAdminId") && raw.Contains("OldSuperAdminId"))
                    {
                        using var doc = JsonDocument.Parse(raw);
                        var root = doc.RootElement;

                        string? newAdminName = null;
                        string? oldAdminName = null;

                        if (root.TryGetProperty("data", out var dataObj)) 
                        {
                            newAdminName = dataObj.TryGetProperty("NewSuperAdminUsername", out var newNameProp)
                                ? newNameProp.GetString()
                                : null;

                            oldAdminName = dataObj.TryGetProperty("OldSuperAdminUsername", out var oldNameProp)
                                ? oldNameProp.GetString()
                                : null;
                        }

                        return !string.IsNullOrEmpty(newAdminName) && !string.IsNullOrEmpty(oldAdminName)
                            ? $"{newAdminName} got promoted to Super Admin by {oldAdminName}."
                            : "A lab member was promoted to Super Admin.";
                    }

                    return "A lab member was promoted to Super Admin.";
                }
                catch
                {
                    return "Super Admin role changed.";
                }
            },


            // Create Member API
            ["/api/labs/members"] = dto =>
            {
                try
                {
                    if (dto is string raw)
                    {
                        using var doc = JsonDocument.Parse(raw);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("data", out var data) &&
                            data.TryGetProperty("notificationContext", out var context))
                        {
                            var memberName = context.TryGetProperty("memberName", out var m) ? m.GetString() : null;
                            var createdByName = context.TryGetProperty("createdByName", out var c) ? c.GetString() : null;

                            return !string.IsNullOrWhiteSpace(memberName) && !string.IsNullOrWhiteSpace(createdByName)
                                ? $"{memberName} was successfully added by {createdByName}."
                                : "A new member was successfully added.";
                        }

                        return "A new member was successfully added.";
                    }

                    return "A new member was successfully added.";
                }
                catch
                {
                    return "Member created.";
                }
            },






            // Promote Member API
            ["/api/labs/members/promote"] = dto =>
            {
                try
                {
                    if (dto is string raw)
                    {
                        using var doc = JsonDocument.Parse(raw);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("data", out var data) &&
                            data.TryGetProperty("NotificationContext", out var context))
                        {
                            var names = context.TryGetProperty("PromotedNames", out var list)
                                ? list.EnumerateArray().Select(p => p.GetString()).Where(n => !string.IsNullOrWhiteSpace(n)).ToList()
                                : [];

                            var promoterName = context.TryGetProperty("PromoterName", out var by)
                                ? by.GetString()
                                : null;

                            if (names.Count > 0 && !string.IsNullOrWhiteSpace(promoterName))
                            {
                                return names.Count == 1
                                    ? $"Member {names[0]} was promoted to Admin by {promoterName}."
                                    : $"Members {string.Join(", ", names)} were promoted to Admin by {promoterName}.";
                            }
                        }
                    }

                    return "Lab members promoted to Admin.";
                }
                catch
                {
                    return "Lab member promotion completed.";
                }
            },


            // Soft Delete Member API
            ["/api/labs/members/{memberId}"] = dto =>
            {
                try
                {
                    if (dto is string raw && raw.Contains("MemberName") && raw.Contains("DeletedByName"))
                    {
                        using var doc = JsonDocument.Parse(raw);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("data", out var data))
                        {
                            var memberName = data.TryGetProperty("MemberName", out var m) ? m.GetString() : null;
                            var deletedByName = data.TryGetProperty("DeletedByName", out var d) ? d.GetString() : null;

                            return !string.IsNullOrWhiteSpace(memberName) && !string.IsNullOrWhiteSpace(deletedByName)
                                ? $"Member {memberName} was removed by {deletedByName}."
                                : "A lab member was deleted.";
                        }
                    }

                    return "A lab member was deleted.";
                }
                catch
                {
                    return "Lab member removal completed.";
                }
            },


            // Revert User API
            ["/api/labs/revert-user"] = dto =>
            {
                try
                {
                    if (dto is string raw && raw.Contains("NotificationContext"))
                    {
                        using var doc = JsonDocument.Parse(raw);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("data", out var data) &&
                            data.TryGetProperty("NotificationContext", out var context))
                        {
                            var name = context.TryGetProperty("ReinstatedName", out var u) ? u.GetString() : null;
                            var byName = context.TryGetProperty("RevertedByName", out var r) ? r.GetString() : null;

                            return !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(byName)
                                ? $"Member {name} was successfully restored by {byName}."
                                : "User was reinstated.";
                        }
                    }
                    return "User was reinstated.";
                }
                catch
                {
                    return "User restoration completed.";
                }
            },


            // Permanent remove User API
            ["/api/labs/remove-user"] = dto =>
            {
                try
                {
                    if (dto is string raw && raw.Contains("NotificationContext"))
                    {
                        using var doc = JsonDocument.Parse(raw);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("data", out var data) &&
                            data.TryGetProperty("NotificationContext", out var context))
                        {
                            var deletedName = context.TryGetProperty("DeletedUserName", out var u) ? u.GetString() : null;
                            var byName = context.TryGetProperty("DeletedByName", out var r) ? r.GetString() : null;

                            return !string.IsNullOrWhiteSpace(deletedName) && !string.IsNullOrWhiteSpace(byName)
                                ? $"Member {deletedName} was permanently deleted by {byName}."
                                : "A user was permanently removed.";
                        }
                    }
                    return "A user was permanently removed.";
                }
                catch
                {
                    return "Permanent deletion completed.";
                }
            }





        };

        public static string GenerateMessage(string route, object? dto)
        {
            return RouteMap.TryGetValue(route.ToLower(), out var generator)
                ? generator(dto)
                : $"Action completed for {route}";
        }
    }
}
