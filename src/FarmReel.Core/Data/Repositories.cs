using System;
using System.Collections.Generic;
using System.Globalization;
using FarmReel.Core.Models;

namespace FarmReel.Core.Data
{
    public static class RowMapper
    {
        public static long GetLong(Dictionary<string, object> row, string key) =>
            row.TryGetValue(key, out var v) && v != null ? Convert.ToInt64(v) : 0;

        public static int GetInt(Dictionary<string, object> row, string key) =>
            row.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : 0;

        public static string GetStr(Dictionary<string, object> row, string key) =>
            row.TryGetValue(key, out var v) && v != null ? Convert.ToString(v) : "";

        public static bool GetBool(Dictionary<string, object> row, string key) => GetInt(row, key) != 0;

        public static DateTime GetDate(Dictionary<string, object> row, string key) =>
            DateTime.TryParse(GetStr(row, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d) ? d : DateTime.MinValue;

        public static DateTime? GetDateNullable(Dictionary<string, object> row, string key)
        {
            var s = GetStr(row, key);
            return string.IsNullOrEmpty(s) ? (DateTime?)null : GetDate(row, key);
        }
    }

    public class AccountRepository
    {
        public List<Account> GetAll()
        {
            var rows = Db.Query("SELECT * FROM Accounts ORDER BY Id");
            var list = new List<Account>();
            foreach (var r in rows) list.Add(Map(r));
            return list;
        }

        public Account Get(long id)
        {
            var rows = Db.Query("SELECT * FROM Accounts WHERE Id=$id", ("$id", id));
            return rows.Count == 0 ? null : Map(rows[0]);
        }

        public long Save(Account a)
        {
            if (a.Id == 0)
            {
                Db.Execute(
                    @"INSERT INTO Accounts(Uid,Email,PasswordEnc,TotpSecretEnc,Phone,DateOfBirth,Name,Status,DeviceId,MailProvider,Notes,CreatedAt,LastCheckAt,DateCreated,ProfessionalMode,ExtraJson)
                      VALUES($uid,$email,$pw,$totp,$phone,$dob,$name,$status,$dev,$mail,$notes,$created,$last,$dc,$pro,$extra)",
                    ("$uid", a.Uid), ("$email", a.Email), ("$pw", a.PasswordEnc), ("$totp", a.TotpSecretEnc),
                    ("$phone", a.Phone), ("$dob", a.DateOfBirth), ("$name", a.Name), ("$status", (int)a.Status),
                    ("$dev", a.DeviceId), ("$mail", a.MailProvider), ("$notes", a.Notes),
                    ("$created", a.CreatedAt.ToString("o")), ("$last", a.LastCheckAt?.ToString("o")),
                    ("$dc", a.DateCreated?.ToString("o")), ("$pro", a.ProfessionalMode ? 1 : 0),
                    ("$extra", a.ExtraJson));
                var rows = Db.Query("SELECT last_insert_rowid() AS Id");
                return rows.Count == 0 ? 0 : RowMapper.GetLong(rows[0], "Id");
            }
            Db.Execute(
                @"UPDATE Accounts SET Uid=$uid,Email=$email,PasswordEnc=$pw,TotpSecretEnc=$totp,Phone=$phone,
                  DateOfBirth=$dob,Name=$name,Status=$status,DeviceId=$dev,MailProvider=$mail,Notes=$notes,
                  LastCheckAt=$last,DateCreated=$dc,ProfessionalMode=$pro,ExtraJson=$extra WHERE Id=$id",
                ("$uid", a.Uid), ("$email", a.Email), ("$pw", a.PasswordEnc), ("$totp", a.TotpSecretEnc),
                ("$phone", a.Phone), ("$dob", a.DateOfBirth), ("$name", a.Name), ("$status", (int)a.Status),
                ("$dev", a.DeviceId), ("$mail", a.MailProvider), ("$notes", a.Notes), ("$id", a.Id),
                ("$last", a.LastCheckAt?.ToString("o")), ("$dc", a.DateCreated?.ToString("o")),
                ("$pro", a.ProfessionalMode ? 1 : 0), ("$extra", a.ExtraJson));
            return a.Id;
        }

        public void Delete(long id) => Db.Execute("DELETE FROM Accounts WHERE Id=$id", ("$id", id));

        public static Account Map(Dictionary<string, object> r) => new Account
        {
            Id = RowMapper.GetLong(r, "Id"),
            Uid = RowMapper.GetStr(r, "Uid"),
            Email = RowMapper.GetStr(r, "Email"),
            PasswordEnc = RowMapper.GetStr(r, "PasswordEnc"),
            TotpSecretEnc = RowMapper.GetStr(r, "TotpSecretEnc"),
            Phone = RowMapper.GetStr(r, "Phone"),
            DateOfBirth = RowMapper.GetStr(r, "DateOfBirth"),
            Name = RowMapper.GetStr(r, "Name"),
            Status = (AccountStatus)RowMapper.GetInt(r, "Status"),
            DeviceId = RowMapper.GetLong(r, "DeviceId"),
            MailProvider = RowMapper.GetStr(r, "MailProvider"),
            Notes = RowMapper.GetStr(r, "Notes"),
            CreatedAt = RowMapper.GetDate(r, "CreatedAt"),
            LastCheckAt = RowMapper.GetDateNullable(r, "LastCheckAt"),
            DateCreated = RowMapper.GetDateNullable(r, "DateCreated"),
            ProfessionalMode = RowMapper.GetBool(r, "ProfessionalMode"),
            ExtraJson = RowMapper.GetStr(r, "ExtraJson")
        };
    }

    public class PageRepository
    {
        public List<Page> GetAll()
        {
            var rows = Db.Query("SELECT * FROM Pages ORDER BY Id");
            var list = new List<Page>();
            foreach (var r in rows) list.Add(Map(r));
            return list;
        }

        public long Save(Page p)
        {
            if (p.Id == 0)
            {
                Db.Execute(
                    @"INSERT INTO Pages(AccountId,PageId,Name,IdentityMode,Status,Followers,Reach,MonetizationEligible,MonetizationNote,SupportInboxJson,DashboardJson,Notes,LastCheckAt)
                      VALUES($acc,$pid,$name,$mode,$status,$fol,$reach,$mon,$mnote,$inbox,$dash,$notes,$last)",
                    ("$acc", p.AccountId), ("$pid", p.PageId), ("$name", p.Name), ("$mode", p.IdentityMode),
                    ("$status", p.Status), ("$fol", p.Followers), ("$reach", p.Reach),
                    ("$mon", p.MonetizationEligible ? 1 : 0), ("$mnote", p.MonetizationNote),
                    ("$inbox", p.SupportInboxJson), ("$dash", p.DashboardJson), ("$notes", p.Notes),
                    ("$last", p.LastCheckAt?.ToString("o")));
                var rows = Db.Query("SELECT last_insert_rowid() AS Id");
                return rows.Count == 0 ? 0 : RowMapper.GetLong(rows[0], "Id");
            }
            Db.Execute(
                @"UPDATE Pages SET AccountId=$acc,PageId=$pid,Name=$name,IdentityMode=$mode,Status=$status,
                  Followers=$fol,Reach=$reach,MonetizationEligible=$mon,MonetizationNote=$mnote,
                  SupportInboxJson=$inbox,DashboardJson=$dash,Notes=$notes,LastCheckAt=$last WHERE Id=$id",
                ("$acc", p.AccountId), ("$pid", p.PageId), ("$name", p.Name), ("$mode", p.IdentityMode),
                ("$status", p.Status), ("$fol", p.Followers), ("$reach", p.Reach),
                ("$mon", p.MonetizationEligible ? 1 : 0), ("$mnote", p.MonetizationNote),
                ("$inbox", p.SupportInboxJson), ("$dash", p.DashboardJson), ("$notes", p.Notes),
                ("$last", p.LastCheckAt?.ToString("o")), ("$id", p.Id));
            return p.Id;
        }

        public void Delete(long id) => Db.Execute("DELETE FROM Pages WHERE Id=$id", ("$id", id));
        public void DeleteByAccount(long accountId) => Db.Execute("DELETE FROM Pages WHERE AccountId=$id", ("$id", accountId));

        public static Page Map(Dictionary<string, object> r) => new Page
        {
            Id = RowMapper.GetLong(r, "Id"),
            AccountId = RowMapper.GetLong(r, "AccountId"),
            PageId = RowMapper.GetStr(r, "PageId"),
            Name = RowMapper.GetStr(r, "Name"),
            IdentityMode = RowMapper.GetStr(r, "IdentityMode"),
            Status = RowMapper.GetStr(r, "Status"),
            Followers = RowMapper.GetLong(r, "Followers"),
            Reach = RowMapper.GetLong(r, "Reach"),
            MonetizationEligible = RowMapper.GetBool(r, "MonetizationEligible"),
            MonetizationNote = RowMapper.GetStr(r, "MonetizationNote"),
            SupportInboxJson = RowMapper.GetStr(r, "SupportInboxJson"),
            DashboardJson = RowMapper.GetStr(r, "DashboardJson"),
            Notes = RowMapper.GetStr(r, "Notes"),
            LastCheckAt = RowMapper.GetDateNullable(r, "LastCheckAt")
        };
    }

    public class DeviceRepository
    {
        public List<DeviceInstance> GetAll()
        {
            var rows = Db.Query("SELECT * FROM Devices ORDER BY Id");
            var list = new List<DeviceInstance>();
            foreach (var r in rows) list.Add(Map(r));
            return list;
        }

        public DeviceInstance Get(long id)
        {
            var rows = Db.Query("SELECT * FROM Devices WHERE Id=$id", ("$id", id));
            return rows.Count == 0 ? null : Map(rows[0]);
        }

        public long Save(DeviceInstance d)
        {
            if (d.Id == 0)
            {
                Db.Execute(
                    @"INSERT INTO Devices(Name,[Index],GroupId,DeviceType,AccountId,PackageName,AdbSerial,Cpu,RamMb,Resolution,Dpi,NetworkBridge,DeviceInfoJson,LastIpJson,VpnProfile,Proxy,Rooted,Enabled,LastRunAt)
                      VALUES($name,$idx,$gid,$type,$acc,$pkg,$serial,$cpu,$ram,$res,$dpi,$bridge,$info,$ip,$vpn,$proxy,$root,$en,$last)",
                    ("$name", d.Name), ("$idx", d.Index), ("$gid", d.GroupId), ("$type", (int)d.DeviceType),
                    ("$acc", d.AccountId), ("$pkg", d.PackageName), ("$serial", d.AdbSerial), ("$cpu", d.Cpu),
                    ("$ram", d.RamMb), ("$res", d.Resolution), ("$dpi", d.Dpi), ("$bridge", d.NetworkBridge ? 1 : 0),
                    ("$info", d.DeviceInfoJson), ("$ip", d.LastIpJson), ("$vpn", d.VpnProfile), ("$proxy", d.Proxy),
                    ("$root", d.Rooted ? 1 : 0), ("$en", d.Enabled ? 1 : 0), ("$last", d.LastRunAt?.ToString("o")));
                var rows = Db.Query("SELECT last_insert_rowid() AS Id");
                return rows.Count == 0 ? 0 : RowMapper.GetLong(rows[0], "Id");
            }
            Db.Execute(
                @"UPDATE Devices SET Name=$name,[Index]=$idx,GroupId=$gid,DeviceType=$type,AccountId=$acc,
                  PackageName=$pkg,AdbSerial=$serial,Cpu=$cpu,RamMb=$ram,Resolution=$res,Dpi=$dpi,
                  NetworkBridge=$bridge,DeviceInfoJson=$info,LastIpJson=$ip,VpnProfile=$vpn,Proxy=$proxy,
                  Rooted=$root,Enabled=$en,LastRunAt=$last WHERE Id=$id",
                ("$name", d.Name), ("$idx", d.Index), ("$gid", d.GroupId), ("$type", (int)d.DeviceType),
                ("$acc", d.AccountId), ("$pkg", d.PackageName), ("$serial", d.AdbSerial), ("$cpu", d.Cpu),
                ("$ram", d.RamMb), ("$res", d.Resolution), ("$dpi", d.Dpi), ("$bridge", d.NetworkBridge ? 1 : 0),
                ("$info", d.DeviceInfoJson), ("$ip", d.LastIpJson), ("$vpn", d.VpnProfile), ("$proxy", d.Proxy),
                ("$root", d.Rooted ? 1 : 0), ("$en", d.Enabled ? 1 : 0), ("$last", d.LastRunAt?.ToString("o")),
                ("$id", d.Id));
            return d.Id;
        }

        public void Delete(long id) => Db.Execute("DELETE FROM Devices WHERE Id=$id", ("$id", id));

        public static DeviceInstance Map(Dictionary<string, object> r) => new DeviceInstance
        {
            Id = RowMapper.GetLong(r, "Id"),
            Name = RowMapper.GetStr(r, "Name"),
            Index = RowMapper.GetInt(r, "Index"),
            GroupId = RowMapper.GetLong(r, "GroupId"),
            DeviceType = (DeviceType)RowMapper.GetInt(r, "DeviceType"),
            AccountId = RowMapper.GetLong(r, "AccountId"),
            PackageName = RowMapper.GetStr(r, "PackageName"),
            AdbSerial = RowMapper.GetStr(r, "AdbSerial"),
            Cpu = RowMapper.GetInt(r, "Cpu"),
            RamMb = RowMapper.GetInt(r, "RamMb"),
            Resolution = RowMapper.GetStr(r, "Resolution"),
            Dpi = RowMapper.GetInt(r, "Dpi"),
            NetworkBridge = RowMapper.GetBool(r, "NetworkBridge"),
            DeviceInfoJson = RowMapper.GetStr(r, "DeviceInfoJson"),
            LastIpJson = RowMapper.GetStr(r, "LastIpJson"),
            VpnProfile = RowMapper.GetStr(r, "VpnProfile"),
            Proxy = RowMapper.GetStr(r, "Proxy"),
            Rooted = RowMapper.GetBool(r, "Rooted"),
            Enabled = RowMapper.GetBool(r, "Enabled"),
            LastRunAt = RowMapper.GetDateNullable(r, "LastRunAt")
        };
    }

    public class PostJobRepository
    {
        public List<PostJob> GetAll()
        {
            var rows = Db.Query("SELECT * FROM PostJobs ORDER BY Id");
            var list = new List<PostJob>();
            foreach (var r in rows) list.Add(Map(r));
            return list;
        }

        public long Save(PostJob j)
        {
            if (j.Id == 0)
            {
                Db.Execute(
                    @"INSERT INTO PostJobs(PageId,DeviceId,PageName,PostType,Enabled,ContentFolder,NumberOfPosts,RandomFolder,RandomFolderRoot,FilenameFilterFirst,FilenameFilterLast,ReelOrVideo,CaptionFromFile,CaptionFile,CaptionText,AiCaption,AiCaptionPrompt,HashtagsFromFile,HashtagsText,CommentEnabled,CommentText,CommentRandom,CommentFile,CommentWithPhoto,CommentPhotoFolder,CommentDeleteAfterUse,AmazonTag,AudioEnabled,MuteOriginalAudio,AiLabel,StoryWithLink,Collaborators,CollaboratorCount,ShareGroupsCount,CheckInLocation,LocationName,AutoLocation,Audience,ScheduleMode,ScheduleCron,DailyLimit,State,LastError,LastRunAt,NextRunAt,PostsToday,LastFile)
                      VALUES($page,$dev,$pname,$ptype,$en,$folder,$num,$rand,$randroot,$ff,$fl,$rov,$capfile,$capfile2,$captext,$aicap,$aiprompt,$htfile,$httext,$cmen,$cmtext,$cmrand,$cmfile,$cmpic,$cmpicf,$cmdel,$amz,$audio,$mute,$ailabel,$slink,$coll,$collcnt,$share,$checkin,$loc,$autoloc,$aud,$smode,$scron,$limit,$state,$err,$last,$next,$today,$lfile)",
                    ("$page", j.PageId), ("$dev", j.DeviceId), ("$pname", j.PageName), ("$ptype", (int)j.PostType),
                    ("$en", j.Enabled ? 1 : 0), ("$folder", j.ContentFolder), ("$num", j.NumberOfPosts),
                    ("$rand", j.RandomFolder ? 1 : 0), ("$randroot", j.RandomFolderRoot),
                    ("$ff", j.FilenameFilterFirst), ("$fl", j.FilenameFilterLast), ("$rov", j.ReelOrVideo ? 1 : 0),
                    ("$capfile", j.CaptionFromFile ? 1 : 0), ("$capfile2", j.CaptionFile), ("$captext", j.CaptionText),
                    ("$aicap", j.AiCaption ? 1 : 0), ("$aiprompt", j.AiCaptionPrompt),
                    ("$htfile", j.HashtagsFromFile ? 1 : 0), ("$httext", j.HashtagsText),
                    ("$cmen", j.CommentEnabled ? 1 : 0), ("$cmtext", j.CommentText),
                    ("$cmrand", j.CommentRandom ? 1 : 0), ("$cmfile", j.CommentFile),
                    ("$cmpic", j.CommentWithPhoto ? 1 : 0), ("$cmpicf", j.CommentPhotoFolder),
                    ("$cmdel", j.CommentDeleteAfterUse ? 1 : 0), ("$amz", j.AmazonTag),
                    ("$audio", j.AudioEnabled ? 1 : 0), ("$mute", j.MuteOriginalAudio ? 1 : 0),
                    ("$ailabel", j.AiLabel ? 1 : 0), ("$slink", j.StoryWithLink ? 1 : 0),
                    ("$coll", j.Collaborators), ("$collcnt", j.CollaboratorCount), ("$share", j.ShareGroupsCount),
                    ("$checkin", j.CheckInLocation ? 1 : 0), ("$loc", j.LocationName), ("$autoloc", j.AutoLocation ? 1 : 0),
                    ("$aud", j.Audience), ("$smode", (int)j.ScheduleMode), ("$scron", j.ScheduleCron),
                    ("$limit", j.DailyLimit), ("$state", j.State), ("$err", j.LastError),
                    ("$last", j.LastRunAt?.ToString("o")), ("$next", j.NextRunAt?.ToString("o")),
                    ("$today", j.PostsToday), ("$lfile", j.LastFile));
                var rows = Db.Query("SELECT last_insert_rowid() AS Id");
                return rows.Count == 0 ? 0 : RowMapper.GetLong(rows[0], "Id");
            }
            Db.Execute(
                @"UPDATE PostJobs SET PageId=$page,DeviceId=$dev,PageName=$pname,PostType=$ptype,Enabled=$en,
                  ContentFolder=$folder,NumberOfPosts=$num,RandomFolder=$rand,RandomFolderRoot=$randroot,
                  FilenameFilterFirst=$ff,FilenameFilterLast=$fl,ReelOrVideo=$rov,
                  CaptionFromFile=$capfile,CaptionFile=$capfile2,CaptionText=$captext,AiCaption=$aicap,AiCaptionPrompt=$aiprompt,
                  HashtagsFromFile=$htfile,HashtagsText=$httext,
                  CommentEnabled=$cmen,CommentText=$cmtext,CommentRandom=$cmrand,CommentFile=$cmfile,
                  CommentWithPhoto=$cmpic,CommentPhotoFolder=$cmpicf,CommentDeleteAfterUse=$cmdel,AmazonTag=$amz,
                  AudioEnabled=$audio,MuteOriginalAudio=$mute,AiLabel=$ailabel,StoryWithLink=$slink,
                  Collaborators=$coll,CollaboratorCount=$collcnt,ShareGroupsCount=$share,
                  CheckInLocation=$checkin,LocationName=$loc,AutoLocation=$autoloc,Audience=$aud,
                  ScheduleMode=$smode,ScheduleCron=$scron,DailyLimit=$limit,
                  State=$state,LastError=$err,LastRunAt=$last,NextRunAt=$next,PostsToday=$today,LastFile=$lfile WHERE Id=$id",
                ("$page", j.PageId), ("$dev", j.DeviceId), ("$pname", j.PageName), ("$ptype", (int)j.PostType),
                ("$en", j.Enabled ? 1 : 0), ("$folder", j.ContentFolder), ("$num", j.NumberOfPosts),
                ("$rand", j.RandomFolder ? 1 : 0), ("$randroot", j.RandomFolderRoot),
                ("$ff", j.FilenameFilterFirst), ("$fl", j.FilenameFilterLast), ("$rov", j.ReelOrVideo ? 1 : 0),
                ("$capfile", j.CaptionFromFile ? 1 : 0), ("$capfile2", j.CaptionFile), ("$captext", j.CaptionText),
                ("$aicap", j.AiCaption ? 1 : 0), ("$aiprompt", j.AiCaptionPrompt),
                ("$htfile", j.HashtagsFromFile ? 1 : 0), ("$httext", j.HashtagsText),
                ("$cmen", j.CommentEnabled ? 1 : 0), ("$cmtext", j.CommentText),
                ("$cmrand", j.CommentRandom ? 1 : 0), ("$cmfile", j.CommentFile),
                ("$cmpic", j.CommentWithPhoto ? 1 : 0), ("$cmpicf", j.CommentPhotoFolder),
                ("$cmdel", j.CommentDeleteAfterUse ? 1 : 0), ("$amz", j.AmazonTag),
                ("$audio", j.AudioEnabled ? 1 : 0), ("$mute", j.MuteOriginalAudio ? 1 : 0),
                ("$ailabel", j.AiLabel ? 1 : 0), ("$slink", j.StoryWithLink ? 1 : 0),
                ("$coll", j.Collaborators), ("$collcnt", j.CollaboratorCount), ("$share", j.ShareGroupsCount),
                ("$checkin", j.CheckInLocation ? 1 : 0), ("$loc", j.LocationName), ("$autoloc", j.AutoLocation ? 1 : 0),
                ("$aud", j.Audience), ("$smode", (int)j.ScheduleMode), ("$scron", j.ScheduleCron),
                ("$limit", j.DailyLimit), ("$state", j.State), ("$err", j.LastError),
                ("$last", j.LastRunAt?.ToString("o")), ("$next", j.NextRunAt?.ToString("o")),
                ("$today", j.PostsToday), ("$lfile", j.LastFile), ("$id", j.Id));
            return j.Id;
        }

        public void Delete(long id) => Db.Execute("DELETE FROM PostJobs WHERE Id=$id", ("$id", id));

        public static PostJob Map(Dictionary<string, object> r) => new PostJob
        {
            Id = RowMapper.GetLong(r, "Id"),
            PageId = RowMapper.GetLong(r, "PageId"),
            DeviceId = RowMapper.GetLong(r, "DeviceId"),
            PageName = RowMapper.GetStr(r, "PageName"),
            PostType = (PostType)RowMapper.GetInt(r, "PostType"),
            Enabled = RowMapper.GetBool(r, "Enabled"),
            ContentFolder = RowMapper.GetStr(r, "ContentFolder"),
            NumberOfPosts = Math.Max(1, RowMapper.GetInt(r, "NumberOfPosts")),
            RandomFolder = RowMapper.GetBool(r, "RandomFolder"),
            RandomFolderRoot = RowMapper.GetStr(r, "RandomFolderRoot"),
            FilenameFilterFirst = RowMapper.GetStr(r, "FilenameFilterFirst"),
            FilenameFilterLast = RowMapper.GetStr(r, "FilenameFilterLast"),
            ReelOrVideo = RowMapper.GetBool(r, "ReelOrVideo"),
            CaptionFromFile = RowMapper.GetBool(r, "CaptionFromFile"),
            CaptionFile = RowMapper.GetStr(r, "CaptionFile"),
            CaptionText = RowMapper.GetStr(r, "CaptionText"),
            AiCaption = RowMapper.GetBool(r, "AiCaption"),
            AiCaptionPrompt = RowMapper.GetStr(r, "AiCaptionPrompt"),
            HashtagsFromFile = RowMapper.GetBool(r, "HashtagsFromFile"),
            HashtagsText = RowMapper.GetStr(r, "HashtagsText"),
            CommentEnabled = RowMapper.GetBool(r, "CommentEnabled"),
            CommentText = RowMapper.GetStr(r, "CommentText"),
            CommentRandom = RowMapper.GetBool(r, "CommentRandom"),
            CommentFile = RowMapper.GetStr(r, "CommentFile"),
            CommentWithPhoto = RowMapper.GetBool(r, "CommentWithPhoto"),
            CommentPhotoFolder = RowMapper.GetStr(r, "CommentPhotoFolder"),
            CommentDeleteAfterUse = RowMapper.GetBool(r, "CommentDeleteAfterUse"),
            AmazonTag = RowMapper.GetStr(r, "AmazonTag"),
            AudioEnabled = RowMapper.GetBool(r, "AudioEnabled"),
            MuteOriginalAudio = RowMapper.GetBool(r, "MuteOriginalAudio"),
            AiLabel = RowMapper.GetBool(r, "AiLabel"),
            StoryWithLink = RowMapper.GetBool(r, "StoryWithLink"),
            Collaborators = RowMapper.GetStr(r, "Collaborators"),
            CollaboratorCount = RowMapper.GetInt(r, "CollaboratorCount"),
            ShareGroupsCount = RowMapper.GetInt(r, "ShareGroupsCount"),
            CheckInLocation = RowMapper.GetBool(r, "CheckInLocation"),
            LocationName = RowMapper.GetStr(r, "LocationName"),
            AutoLocation = RowMapper.GetBool(r, "AutoLocation"),
            Audience = RowMapper.GetStr(r, "Audience"),
            ScheduleMode = (ScheduleMode)RowMapper.GetInt(r, "ScheduleMode"),
            ScheduleCron = RowMapper.GetStr(r, "ScheduleCron"),
            DailyLimit = RowMapper.GetInt(r, "DailyLimit"),
            State = RowMapper.GetStr(r, "State"),
            LastError = RowMapper.GetStr(r, "LastError"),
            LastRunAt = RowMapper.GetDateNullable(r, "LastRunAt"),
            NextRunAt = RowMapper.GetDateNullable(r, "NextRunAt"),
            PostsToday = RowMapper.GetLong(r, "PostsToday"),
            LastFile = RowMapper.GetStr(r, "LastFile")
        };
    }

    public class InteractionJobRepository
    {
        public List<InteractionJob> GetAll()
        {
            var rows = Db.Query("SELECT * FROM InteractionJobs ORDER BY Id");
            var list = new List<InteractionJob>();
            foreach (var r in rows) list.Add(Map(r));
            return list;
        }

        public long Save(InteractionJob j)
        {
            if (j.Id == 0)
            {
                Db.Execute(
                    @"INSERT INTO InteractionJobs(DeviceId,PageId,Kind,Enabled,MaxActions,MaxLikes,MaxComments,MaxFollows,MaxShares,MinDelaySec,MaxDelaySec,ReactRandom,CommentRandom,CommentFile,CommentDeleteAfterUse,LikeAndFollow,CheckInPost,AutoLocation,PolicyJson,State,LastError,LastRunAt)
                      VALUES($dev,$page,$kind,$en,$maxa,$maxl,$maxc,$maxf,$maxs,$mind,$maxd,$rr,$cr,$cf,$cd,$lf,$ck,$al,$pol,$state,$err,$last)",
                    ("$dev", j.DeviceId), ("$page", j.PageId), ("$kind", j.Kind), ("$en", j.Enabled ? 1 : 0),
                    ("$maxa", j.MaxActions), ("$maxl", j.MaxLikes), ("$maxc", j.MaxComments), ("$maxf", j.MaxFollows),
                    ("$maxs", j.MaxShares), ("$mind", j.MinDelaySec), ("$maxd", j.MaxDelaySec),
                    ("$rr", j.ReactRandom ? 1 : 0), ("$cr", j.CommentRandom ? 1 : 0), ("$cf", j.CommentFile),
                    ("$cd", j.CommentDeleteAfterUse ? 1 : 0), ("$lf", j.LikeAndFollow ? 1 : 0),
                    ("$ck", j.CheckInPost ? 1 : 0), ("$al", j.AutoLocation ? 1 : 0), ("$pol", j.PolicyJson),
                    ("$state", j.State), ("$err", j.LastError), ("$last", j.LastRunAt?.ToString("o")));
                var rows = Db.Query("SELECT last_insert_rowid() AS Id");
                return rows.Count == 0 ? 0 : RowMapper.GetLong(rows[0], "Id");
            }
            Db.Execute(
                @"UPDATE InteractionJobs SET DeviceId=$dev,PageId=$page,Kind=$kind,Enabled=$en,
                  MaxActions=$maxa,MaxLikes=$maxl,MaxComments=$maxc,MaxFollows=$maxf,MaxShares=$maxs,
                  MinDelaySec=$mind,MaxDelaySec=$maxd,ReactRandom=$rr,CommentRandom=$cr,CommentFile=$cf,
                  CommentDeleteAfterUse=$cd,LikeAndFollow=$lf,CheckInPost=$ck,AutoLocation=$al,
                  PolicyJson=$pol,State=$state,LastError=$err,LastRunAt=$last WHERE Id=$id",
                ("$dev", j.DeviceId), ("$page", j.PageId), ("$kind", j.Kind), ("$en", j.Enabled ? 1 : 0),
                ("$maxa", j.MaxActions), ("$maxl", j.MaxLikes), ("$maxc", j.MaxComments), ("$maxf", j.MaxFollows),
                ("$maxs", j.MaxShares), ("$mind", j.MinDelaySec), ("$maxd", j.MaxDelaySec),
                ("$rr", j.ReactRandom ? 1 : 0), ("$cr", j.CommentRandom ? 1 : 0), ("$cf", j.CommentFile),
                ("$cd", j.CommentDeleteAfterUse ? 1 : 0), ("$lf", j.LikeAndFollow ? 1 : 0),
                ("$ck", j.CheckInPost ? 1 : 0), ("$al", j.AutoLocation ? 1 : 0), ("$pol", j.PolicyJson),
                ("$state", j.State), ("$err", j.LastError), ("$last", j.LastRunAt?.ToString("o")), ("$id", j.Id));
            return j.Id;
        }

        public void Delete(long id) => Db.Execute("DELETE FROM InteractionJobs WHERE Id=$id", ("$id", id));

        public static InteractionJob Map(Dictionary<string, object> r) => new InteractionJob
        {
            Id = RowMapper.GetLong(r, "Id"),
            DeviceId = RowMapper.GetLong(r, "DeviceId"),
            PageId = RowMapper.GetLong(r, "PageId"),
            Kind = RowMapper.GetStr(r, "Kind"),
            Enabled = RowMapper.GetBool(r, "Enabled"),
            MaxActions = RowMapper.GetInt(r, "MaxActions"),
            MaxLikes = RowMapper.GetInt(r, "MaxLikes"),
            MaxComments = RowMapper.GetInt(r, "MaxComments"),
            MaxFollows = RowMapper.GetInt(r, "MaxFollows"),
            MaxShares = RowMapper.GetInt(r, "MaxShares"),
            MinDelaySec = RowMapper.GetInt(r, "MinDelaySec"),
            MaxDelaySec = RowMapper.GetInt(r, "MaxDelaySec"),
            ReactRandom = RowMapper.GetBool(r, "ReactRandom"),
            CommentRandom = RowMapper.GetBool(r, "CommentRandom"),
            CommentFile = RowMapper.GetStr(r, "CommentFile"),
            CommentDeleteAfterUse = RowMapper.GetBool(r, "CommentDeleteAfterUse"),
            LikeAndFollow = RowMapper.GetBool(r, "LikeAndFollow"),
            CheckInPost = RowMapper.GetBool(r, "CheckInPost"),
            AutoLocation = RowMapper.GetBool(r, "AutoLocation"),
            PolicyJson = RowMapper.GetStr(r, "PolicyJson"),
            State = RowMapper.GetStr(r, "State"),
            LastError = RowMapper.GetStr(r, "LastError"),
            LastRunAt = RowMapper.GetDateNullable(r, "LastRunAt")
        };
    }

    public class EmailRepository
    {
        public List<EmailAccount> GetAll()
        {
            var rows = Db.Query("SELECT * FROM Emails ORDER BY Id");
            var list = new List<EmailAccount>();
            foreach (var r in rows) list.Add(Map(r));
            return list;
        }

        public long Save(EmailAccount e)
        {
            if (e.Id == 0)
            {
                Db.Execute(
                    @"INSERT INTO Emails(Email,PasswordEnc,Provider,IsTrusted,Notes,CreatedAt) VALUES($em,$pw,$prov,$trust,$notes,$created)",
                    ("$em", e.Email), ("$pw", e.PasswordEnc), ("$prov", e.Provider), ("$trust", e.IsTrusted ? 1 : 0),
                    ("$notes", e.Notes), ("$created", e.CreatedAt.ToString("o")));
                var rows = Db.Query("SELECT last_insert_rowid() AS Id");
                return rows.Count == 0 ? 0 : RowMapper.GetLong(rows[0], "Id");
            }
            Db.Execute(
                @"UPDATE Emails SET Email=$em,PasswordEnc=$pw,Provider=$prov,IsTrusted=$trust,Notes=$notes WHERE Id=$id",
                ("$em", e.Email), ("$pw", e.PasswordEnc), ("$prov", e.Provider), ("$trust", e.IsTrusted ? 1 : 0),
                ("$notes", e.Notes), ("$id", e.Id));
            return e.Id;
        }

        public void Delete(long id) => Db.Execute("DELETE FROM Emails WHERE Id=$id", ("$id", id));

        public static EmailAccount Map(Dictionary<string, object> r) => new EmailAccount
        {
            Id = RowMapper.GetLong(r, "Id"),
            Email = RowMapper.GetStr(r, "Email"),
            PasswordEnc = RowMapper.GetStr(r, "PasswordEnc"),
            Provider = RowMapper.GetStr(r, "Provider"),
            IsTrusted = RowMapper.GetBool(r, "IsTrusted"),
            Notes = RowMapper.GetStr(r, "Notes"),
            CreatedAt = RowMapper.GetDate(r, "CreatedAt")
        };
    }

    public class GroupRepository
    {
        public List<LdGroup> GetAll()
        {
            var rows = Db.Query("SELECT * FROM LdGroups ORDER BY Name");
            var list = new List<LdGroup>();
            foreach (var r in rows) list.Add(Map(r));
            return list;
        }

        public long Save(LdGroup g)
        {
            if (g.Id == 0)
            {
                Db.Execute("INSERT INTO LdGroups(Name,Notes) VALUES($name,$notes)",
                    ("$name", g.Name), ("$notes", g.Notes));
                var rows = Db.Query("SELECT last_insert_rowid() AS Id");
                return rows.Count == 0 ? 0 : RowMapper.GetLong(rows[0], "Id");
            }
            Db.Execute("UPDATE LdGroups SET Name=$name,Notes=$notes WHERE Id=$id",
                ("$name", g.Name), ("$notes", g.Notes), ("$id", g.Id));
            return g.Id;
        }

        public void Delete(long id) => Db.Execute("DELETE FROM LdGroups WHERE Id=$id", ("$id", id));

        public static LdGroup Map(Dictionary<string, object> r) => new LdGroup
        {
            Id = RowMapper.GetLong(r, "Id"),
            Name = RowMapper.GetStr(r, "Name"),
            Notes = RowMapper.GetStr(r, "Notes")
        };
    }

    public class TemplateRepository
    {
        public List<Template> GetAll()
        {
            var rows = Db.Query("SELECT * FROM Templates ORDER BY Name");
            var list = new List<Template>();
            foreach (var r in rows) list.Add(Map(r));
            return list;
        }

        public long Save(Template t)
        {
            if (t.Id == 0)
            {
                Db.Execute("INSERT INTO Templates(Name,Category,Json,Notes,CreatedAt) VALUES($name,$cat,$json,$notes,$created)",
                    ("$name", t.Name), ("$cat", t.Category), ("$json", t.Json), ("$notes", t.Notes),
                    ("$created", t.CreatedAt.ToString("o")));
                var rows = Db.Query("SELECT last_insert_rowid() AS Id");
                return rows.Count == 0 ? 0 : RowMapper.GetLong(rows[0], "Id");
            }
            Db.Execute("UPDATE Templates SET Name=$name,Category=$cat,Json=$json,Notes=$notes WHERE Id=$id",
                ("$name", t.Name), ("$cat", t.Category), ("$json", t.Json), ("$notes", t.Notes), ("$id", t.Id));
            return t.Id;
        }

        public void Delete(long id) => Db.Execute("DELETE FROM Templates WHERE Id=$id", ("$id", id));

        public static Template Map(Dictionary<string, object> r) => new Template
        {
            Id = RowMapper.GetLong(r, "Id"),
            Name = RowMapper.GetStr(r, "Name"),
            Category = RowMapper.GetStr(r, "Category"),
            Json = RowMapper.GetStr(r, "Json"),
            Notes = RowMapper.GetStr(r, "Notes"),
            CreatedAt = RowMapper.GetDate(r, "CreatedAt")
        };
    }

    public class SettingsRepository
    {
        public string Get(string key, string def = "")
        {
            var rows = Db.Query("SELECT Value FROM Settings WHERE Key=$key", ("$key", key));
            return rows.Count == 0 ? def : RowMapper.GetStr(rows[0], "Value");
        }

        public void Set(string key, string value)
        {
            var rows = Db.Query("SELECT Key FROM Settings WHERE Key=$key", ("$key", key));
            if (rows.Count == 0)
                Db.Execute("INSERT INTO Settings(Key,Value) VALUES($key,$value)", ("$key", key), ("$value", value));
            else
                Db.Execute("UPDATE Settings SET Value=$value WHERE Key=$key", ("$value", value), ("$key", key));
        }

        public int GetInt(string key, int def = 0) =>
            int.TryParse(Get(key, def.ToString()), out var v) ? v : def;

        public bool GetBool(string key, bool def = false)
        {
            var s = Get(key, def ? "true" : "false");
            return bool.TryParse(s, out var b) ? b : s == "1";
        }

        public Dictionary<string, string> GetAll()
        {
            var rows = Db.Query("SELECT Key, Value FROM Settings");
            var map = new Dictionary<string, string>();
            foreach (var r in rows) map[RowMapper.GetStr(r, "Key")] = RowMapper.GetStr(r, "Value");
            return map;
        }
    }

    public class LogRepository
    {
        public void Insert(LogEntry e) =>
            Db.Execute(
                @"INSERT INTO Logs(Timestamp,Level,Device,Account,Action,Message,ScreenshotPath)
                  VALUES($ts,$level,$dev,$acc,$action,$msg,$shot)",
                ("$ts", e.Timestamp.ToString("o")), ("$level", e.Level), ("$dev", e.Device),
                ("$acc", e.Account), ("$action", e.Action), ("$msg", e.Message), ("$shot", e.ScreenshotPath));

        public List<LogEntry> GetRecent(int limit = 500)
        {
            var rows = Db.Query("SELECT * FROM Logs ORDER BY Id DESC LIMIT $limit", ("$limit", limit));
            rows.Reverse();
            var list = new List<LogEntry>();
            foreach (var r in rows) list.Add(Map(r));
            return list;
        }

        public void Cleanup(int keepDays = 30) =>
            Db.Execute("DELETE FROM Logs WHERE Timestamp < $cutoff",
                ("$cutoff", DateTime.Now.AddDays(-keepDays).ToString("o")));

        public static LogEntry Map(Dictionary<string, object> r) => new LogEntry
        {
            Id = RowMapper.GetLong(r, "Id"),
            Timestamp = RowMapper.GetDate(r, "Timestamp"),
            Level = RowMapper.GetStr(r, "Level"),
            Device = RowMapper.GetStr(r, "Device"),
            Account = RowMapper.GetStr(r, "Account"),
            Action = RowMapper.GetStr(r, "Action"),
            Message = RowMapper.GetStr(r, "Message"),
            ScreenshotPath = RowMapper.GetStr(r, "ScreenshotPath")
        };
    }

    public class ScheduledTaskRepository
    {
        public List<ScheduledTask> GetAll()
        {
            var rows = Db.Query("SELECT * FROM ScheduledTasks ORDER BY Id");
            var list = new List<ScheduledTask>();
            foreach (var r in rows) list.Add(Map(r));
            return list;
        }

        public long Save(ScheduledTask t)
        {
            if (t.Id == 0)
            {
                Db.Execute(
                    @"INSERT INTO ScheduledTasks(Name,Cron,TargetType,TargetId,Enabled,LastRunAt,NextRunAt)
                      VALUES($name,$cron,$type,$tid,$en,$last,$next)",
                    ("$name", t.Name), ("$cron", t.Cron), ("$type", t.TargetType), ("$tid", t.TargetId),
                    ("$en", t.Enabled ? 1 : 0), ("$last", t.LastRunAt?.ToString("o")), ("$next", t.NextRunAt?.ToString("o")));
                var rows = Db.Query("SELECT last_insert_rowid() AS Id");
                return rows.Count == 0 ? 0 : RowMapper.GetLong(rows[0], "Id");
            }
            Db.Execute(
                @"UPDATE ScheduledTasks SET Name=$name,Cron=$cron,TargetType=$type,TargetId=$tid,Enabled=$en,LastRunAt=$last,NextRunAt=$next WHERE Id=$id",
                ("$name", t.Name), ("$cron", t.Cron), ("$type", t.TargetType), ("$tid", t.TargetId),
                ("$en", t.Enabled ? 1 : 0), ("$last", t.LastRunAt?.ToString("o")), ("$next", t.NextRunAt?.ToString("o")),
                ("$id", t.Id));
            return t.Id;
        }

        public void Delete(long id) => Db.Execute("DELETE FROM ScheduledTasks WHERE Id=$id", ("$id", id));

        public static ScheduledTask Map(Dictionary<string, object> r) => new ScheduledTask
        {
            Id = RowMapper.GetLong(r, "Id"),
            Name = RowMapper.GetStr(r, "Name"),
            Cron = RowMapper.GetStr(r, "Cron"),
            TargetType = RowMapper.GetStr(r, "TargetType"),
            TargetId = RowMapper.GetLong(r, "TargetId"),
            Enabled = RowMapper.GetBool(r, "Enabled"),
            LastRunAt = RowMapper.GetDateNullable(r, "LastRunAt"),
            NextRunAt = RowMapper.GetDateNullable(r, "NextRunAt")
        };
    }
}
