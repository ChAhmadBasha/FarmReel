using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FarmReel.Core.Data;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>Page manager operations: CRUD, dashboard pull, monetization, delete, share, groups.</summary>
    public class PageService
    {
        private readonly PageRepository _pages;
        private readonly AccountRepository _accounts;
        private readonly DeviceRepository _devices;
        private readonly PostJobRepository _postJobs;
        private readonly IFlowRunner _flows;

        public PageService(PageRepository pages, AccountRepository accounts, DeviceRepository devices,
            PostJobRepository postJobs, IFlowRunner flows)
        {
            _pages = pages;
            _accounts = accounts;
            _devices = devices;
            _postJobs = postJobs;
            _flows = flows;
        }

        public List<Page> GetAll() => _pages.GetAll();
        public Page Get(long id) => _pages.Get(id);
        public long Save(Page p) => _pages.Save(p);
        public void Delete(long id) { _postJobs.DeleteByPage(id); _pages.Delete(id); }

        /// <summary>Bulk edit: apply field/value to selected pages (pure data operation).</summary>
        public int BulkEdit(IEnumerable<long> pageIds, string field, string value)
        {
            var ids = pageIds.ToHashSet();
            var count = 0;
            foreach (var p in _pages.GetAll().Where(p => ids.Contains(p.Id)))
            {
                switch (field.ToLowerInvariant())
                {
                    case "status": p.Status = value; break;
                    case "identitymode": p.IdentityMode = value; break;
                    case "monetization": p.MonetizationEligible = value == "true" || value == "1"; break;
                    case "notes": p.Notes = value; break;
                    default: continue;
                }
                _pages.Save(p);
                count++;
            }
            return count;
        }

        public async Task<FlowResult> PullDashboardAsync(Page page)
        {
            var account = _accounts.Get(page.AccountId);
            var device = _devices.Get(account?.DeviceId ?? 0);
            var result = await _flows.RunActionAsync(device, account, "page_dashboard",
                new Dictionary<string, string> { ["page_id"] = page.PageId, ["page"] = page.Name }).ConfigureAwait(false);
            if (result.Success && result.Data.TryGetValue("followers", out var f) &&
                long.TryParse(f, out var followers))
            {
                page.Followers = followers;
                page.LastCheckAt = System.DateTime.Now;
                if (result.Data.TryGetValue("reach", out var r) && long.TryParse(r, out var reach))
                    page.Reach = reach;
                if (result.Data.TryGetValue("monetization", out var m))
                {
                    page.MonetizationEligible = m == "true" || m == "1";
                    page.MonetizationNote = result.Data.TryGetValue("monetization_note", out var mn) ? mn : "";
                }
                if (result.Data.TryGetValue("status", out var st)) page.Status = st;
                // Store waitlist/recommendations/extra signals into the dashboard snapshot
                var dash = new Dictionary<string, object>();
                foreach (var k in new[] { "waitlist", "recommendations", "notRecommended", "flagged", "supportInbox" })
                    if (result.Data.TryGetValue(k, out var v)) dash[k] = v;
                if (dash.Count > 0)
                    page.DashboardJson = System.Text.Json.JsonSerializer.Serialize(dash);
                _pages.Save(page);
            }
            return result;
        }

        /// <summary>Check Creator Monetization (CM) waitlist eligibility + recommendations.</summary>
        public async Task<FlowResult> CheckMonetizationAsync(Page page)
        {
            var account = _accounts.Get(page.AccountId);
            var device = _devices.Get(account?.DeviceId ?? 0);
            var result = await _flows.RunActionAsync(device, account, "page_dashboard",
                PageVars(page, ("section", "monetization"))).ConfigureAwait(false);
            if (result.Success)
            {
                if (result.Data.TryGetValue("monetization", out var m))
                    page.MonetizationEligible = m == "true" || m == "1";
                if (result.Data.TryGetValue("monetization_note", out var mn))
                    page.MonetizationNote = mn;
                _pages.Save(page);
            }
            return result;
        }

        private Dictionary<string, string> PageVars(Page page, params (string k, string v)[] extra)
        {
            var vars = new Dictionary<string, string> { ["page_id"] = page.PageId, ["page"] = page.Name };
            foreach (var (k, v) in extra) vars[k] = v;
            return vars;
        }

        public async Task<FlowResult> DeleteAllPostsAsync(Page page) =>
            await _flows.RunActionAsync(_devices.Get(_accounts.Get(page.AccountId)?.DeviceId ?? 0),
                _accounts.Get(page.AccountId), "delete_all_posts", PageVars(page)).ConfigureAwait(false);

        public async Task<FlowResult> SharePostAsync(Page page, string postLink, bool toProfile, bool toGroups, int groupCount) =>
            await _flows.RunActionAsync(_devices.Get(_accounts.Get(page.AccountId)?.DeviceId ?? 0),
                _accounts.Get(page.AccountId), "share_post",
                PageVars(page, ("link", postLink), ("profile", toProfile.ToString()),
                    ("groups", toGroups.ToString()), ("count", groupCount.ToString()))).ConfigureAwait(false);

        public async Task<FlowResult> JoinGroupAsync(Page page, string groupIdOrLink, bool byKeyword, string keyword) =>
            await _flows.RunActionAsync(_devices.Get(_accounts.Get(page.AccountId)?.DeviceId ?? 0),
                _accounts.Get(page.AccountId), "join_group",
                PageVars(page, ("group", groupIdOrLink), ("keyword", keyword), ("search", byKeyword.ToString())))
                .ConfigureAwait(false);

        public async Task<FlowResult> SetPageInfoAsync(Page page, string field, string value) =>
            await _flows.RunActionAsync(_devices.Get(_accounts.Get(page.AccountId)?.DeviceId ?? 0),
                _accounts.Get(page.AccountId), "set_page_info",
                PageVars(page, ("field", field), ("value", value))).ConfigureAwait(false);

        public async Task<FlowResult> AutoDeleteCopyrightAsync(Page page) =>
            await _flows.RunActionAsync(_devices.Get(_accounts.Get(page.AccountId)?.DeviceId ?? 0),
                _accounts.Get(page.AccountId), "auto_delete_copyright", PageVars(page)).ConfigureAwait(false);

        public async Task<FlowResult> DeletePageAsync(Page page) =>
            await _flows.RunActionAsync(_devices.Get(_accounts.Get(page.AccountId)?.DeviceId ?? 0),
                _accounts.Get(page.AccountId), "delete_page", PageVars(page)).ConfigureAwait(false);
    }

    public static class PostJobExtensions
    {
        public static void DeleteByPage(this PostJobRepository repo, long pageId)
            => Db.Execute("DELETE FROM PostJobs WHERE PageId=$id", ("$id", pageId));
    }
}
