using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace FarmReel.Core.Data
{
    /// <summary>Central SQLite access. One connection per operation (SQLite is file-locked anyway).</summary>
    public static class Db
    {
        public static string DatabasePath { get; private set; } = "farmreel.db";

        public static void Initialize(string dbPath)
        {
            DatabasePath = dbPath;
            var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var conn = Open();
            foreach (var statement in Schema.Statements)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = statement;
                cmd.ExecuteNonQuery();
            }
        }

        public static SqliteConnection Open()
        {
            var conn = new SqliteConnection($"Data Source={DatabasePath}");
            conn.Open();
            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
            pragma.ExecuteNonQuery();
            return conn;
        }

        public static List<Dictionary<string, object>> Query(string sql, params (string name, object value)[] parms)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            var rows = new List<Dictionary<string, object>>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }

        public static int Execute(string sql, params (string name, object value)[] parms)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parms)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return cmd.ExecuteNonQuery();
        }

        public static long LastInsertRowId(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT last_insert_rowid();";
            return Convert.ToInt64(cmd.ExecuteScalar());
        }
    }

    public static class Schema
    {
        public static readonly string[] Statements =
        {
            @"CREATE TABLE IF NOT EXISTS Accounts(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Uid TEXT NOT NULL DEFAULT '', Email TEXT NOT NULL DEFAULT '',
                PasswordEnc TEXT NOT NULL DEFAULT '', TotpSecretEnc TEXT NOT NULL DEFAULT '',
                Phone TEXT NOT NULL DEFAULT '', DateOfBirth TEXT NOT NULL DEFAULT '',
                Name TEXT NOT NULL DEFAULT '', Status INTEGER NOT NULL DEFAULT 0,
                DeviceId INTEGER NOT NULL DEFAULT 0, MailProvider TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL,
                LastCheckAt TEXT NULL, DateCreated TEXT NULL,
                ProfessionalMode INTEGER NOT NULL DEFAULT 0, ExtraJson TEXT NOT NULL DEFAULT '{}')",
            @"CREATE TABLE IF NOT EXISTS Pages(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AccountId INTEGER NOT NULL DEFAULT 0, PageId TEXT NOT NULL DEFAULT '',
                Name TEXT NOT NULL DEFAULT '', IdentityMode TEXT NOT NULL DEFAULT 'page',
                Status TEXT NOT NULL DEFAULT 'unknown', Followers INTEGER NOT NULL DEFAULT 0,
                Reach INTEGER NOT NULL DEFAULT 0, MonetizationEligible INTEGER NOT NULL DEFAULT 0,
                MonetizationNote TEXT NOT NULL DEFAULT '', SupportInboxJson TEXT NOT NULL DEFAULT '[]',
                DashboardJson TEXT NOT NULL DEFAULT '{}', Notes TEXT NOT NULL DEFAULT '',
                LastCheckAt TEXT NULL)",
            @"CREATE TABLE IF NOT EXISTS Devices(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL DEFAULT '', [Index] INTEGER NOT NULL DEFAULT 0,
                GroupId INTEGER NOT NULL DEFAULT 0, DeviceType INTEGER NOT NULL DEFAULT 0,
                AccountId INTEGER NOT NULL DEFAULT 0, PackageName TEXT NOT NULL DEFAULT 'com.facebook.katana',
                AdbSerial TEXT NOT NULL DEFAULT '', Cpu INTEGER NOT NULL DEFAULT 2,
                RamMb INTEGER NOT NULL DEFAULT 2048, Resolution TEXT NOT NULL DEFAULT '1080x1920',
                Dpi INTEGER NOT NULL DEFAULT 480, NetworkBridge INTEGER NOT NULL DEFAULT 0,
                DeviceInfoJson TEXT NOT NULL DEFAULT '{}', LastIpJson TEXT NOT NULL DEFAULT '{}',
                VpnProfile TEXT NOT NULL DEFAULT '', Proxy TEXT NOT NULL DEFAULT '',
                Rooted INTEGER NOT NULL DEFAULT 0, Enabled INTEGER NOT NULL DEFAULT 1,
                LastRunAt TEXT NULL)",
            @"CREATE TABLE IF NOT EXISTS LdGroups(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL DEFAULT '', Notes TEXT NOT NULL DEFAULT '')",
            @"CREATE TABLE IF NOT EXISTS Emails(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Email TEXT NOT NULL DEFAULT '', PasswordEnc TEXT NOT NULL DEFAULT '',
                Provider TEXT NOT NULL DEFAULT '', IsTrusted INTEGER NOT NULL DEFAULT 0,
                Notes TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL)",
            @"CREATE TABLE IF NOT EXISTS PostJobs(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PageId INTEGER NOT NULL DEFAULT 0, DeviceId INTEGER NOT NULL DEFAULT 0,
                PageName TEXT NOT NULL DEFAULT '', PostType INTEGER NOT NULL DEFAULT 0,
                Enabled INTEGER NOT NULL DEFAULT 1,
                ContentFolder TEXT NOT NULL DEFAULT '', NumberOfPosts INTEGER NOT NULL DEFAULT 1,
                RandomFolder INTEGER NOT NULL DEFAULT 0, RandomFolderRoot TEXT NOT NULL DEFAULT '',
                FilenameFilterFirst TEXT NOT NULL DEFAULT '', FilenameFilterLast TEXT NOT NULL DEFAULT '',
                ReelOrVideo INTEGER NOT NULL DEFAULT 0,
                CaptionFromFile INTEGER NOT NULL DEFAULT 0, CaptionFile TEXT NOT NULL DEFAULT '',
                CaptionText TEXT NOT NULL DEFAULT '', AiCaption INTEGER NOT NULL DEFAULT 0,
                AiCaptionPrompt TEXT NOT NULL DEFAULT '',
                HashtagsFromFile INTEGER NOT NULL DEFAULT 0, HashtagsText TEXT NOT NULL DEFAULT '',
                CommentEnabled INTEGER NOT NULL DEFAULT 0, CommentText TEXT NOT NULL DEFAULT '',
                CommentRandom INTEGER NOT NULL DEFAULT 0, CommentFile TEXT NOT NULL DEFAULT '',
                CommentWithPhoto INTEGER NOT NULL DEFAULT 0, CommentPhotoFolder TEXT NOT NULL DEFAULT '',
                CommentDeleteAfterUse INTEGER NOT NULL DEFAULT 0, AmazonTag TEXT NOT NULL DEFAULT '',
                AudioEnabled INTEGER NOT NULL DEFAULT 0, MuteOriginalAudio INTEGER NOT NULL DEFAULT 1,
                AiLabel INTEGER NOT NULL DEFAULT 0, StoryWithLink INTEGER NOT NULL DEFAULT 0,
                Collaborators TEXT NOT NULL DEFAULT '', CollaboratorCount INTEGER NOT NULL DEFAULT 0,
                ShareGroupsCount INTEGER NOT NULL DEFAULT 0,
                CheckInLocation INTEGER NOT NULL DEFAULT 0, LocationName TEXT NOT NULL DEFAULT '',
                AutoLocation INTEGER NOT NULL DEFAULT 0, Audience TEXT NOT NULL DEFAULT 'public',
                ScheduleMode INTEGER NOT NULL DEFAULT 0, ScheduleCron TEXT NOT NULL DEFAULT '',
                DailyLimit INTEGER NOT NULL DEFAULT 0,
                State TEXT NOT NULL DEFAULT 'idle', LastError TEXT NOT NULL DEFAULT '',
                LastRunAt TEXT NULL, NextRunAt TEXT NULL, PostsToday INTEGER NOT NULL DEFAULT 0,
                LastFile TEXT NOT NULL DEFAULT '')",
            @"CREATE TABLE IF NOT EXISTS InteractionJobs(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DeviceId INTEGER NOT NULL DEFAULT 0, PageId INTEGER NOT NULL DEFAULT 0,
                Kind TEXT NOT NULL DEFAULT 'feed', Enabled INTEGER NOT NULL DEFAULT 1,
                MaxActions INTEGER NOT NULL DEFAULT 30, MaxLikes INTEGER NOT NULL DEFAULT 20,
                MaxComments INTEGER NOT NULL DEFAULT 5, MaxFollows INTEGER NOT NULL DEFAULT 5,
                MaxShares INTEGER NOT NULL DEFAULT 3, MinDelaySec INTEGER NOT NULL DEFAULT 3,
                MaxDelaySec INTEGER NOT NULL DEFAULT 12,
                ReactRandom INTEGER NOT NULL DEFAULT 1, CommentRandom INTEGER NOT NULL DEFAULT 1,
                CommentFile TEXT NOT NULL DEFAULT '', CommentDeleteAfterUse INTEGER NOT NULL DEFAULT 0,
                LikeAndFollow INTEGER NOT NULL DEFAULT 1, CheckInPost INTEGER NOT NULL DEFAULT 0,
                AutoLocation INTEGER NOT NULL DEFAULT 0, PolicyJson TEXT NOT NULL DEFAULT '{}',
                State TEXT NOT NULL DEFAULT 'idle', LastError TEXT NOT NULL DEFAULT '',
                LastRunAt TEXT NULL)",
            @"CREATE TABLE IF NOT EXISTS ScheduledTasks(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL DEFAULT '', Cron TEXT NOT NULL DEFAULT '',
                TargetType TEXT NOT NULL DEFAULT 'post', TargetId INTEGER NOT NULL DEFAULT 0,
                Enabled INTEGER NOT NULL DEFAULT 1, LastRunAt TEXT NULL, NextRunAt TEXT NULL)",
            @"CREATE TABLE IF NOT EXISTS Templates(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL DEFAULT '', Category TEXT NOT NULL DEFAULT 'post',
                Json TEXT NOT NULL DEFAULT '{}', Notes TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL)",
            @"CREATE TABLE IF NOT EXISTS Settings(
                Key TEXT PRIMARY KEY, Value TEXT NOT NULL DEFAULT '')",
            @"CREATE TABLE IF NOT EXISTS Logs(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL, Level TEXT NOT NULL DEFAULT 'INFO',
                Device TEXT NOT NULL DEFAULT '', Account TEXT NOT NULL DEFAULT '',
                Action TEXT NOT NULL DEFAULT '', Message TEXT NOT NULL DEFAULT '',
                ScreenshotPath TEXT NOT NULL DEFAULT '')",
            @"CREATE INDEX IF NOT EXISTS IX_Logs_Timestamp ON Logs(Timestamp)",
            @"CREATE INDEX IF NOT EXISTS IX_Accounts_Status ON Accounts(Status)",
            @"CREATE INDEX IF NOT EXISTS IX_PostJobs_Page ON PostJobs(PageId)"
        };
    }
}
