using System.Collections.Generic;

namespace FarmReel.Automation
{
    /// <summary>
    /// Additional built-in FlowScript definitions for account/page/groups/monitoring actions.
    /// These are best-effort selectors; tune per Facebook app version or override with
    /// JSON files in the flows folder.
    /// </summary>
    public static partial class DefaultFlows
    {
        public static Dictionary<string, string> BuildExtra()
        {
            var map = new Dictionary<string, string>
            {
                ["pull_names"] = @"
{
  ""name"": ""pull_names"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""what's on your mind|menu|profile"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""menu|more"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""profile|account"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""edit profile|see your about info|friends"" }, ""timeout"": 25, ""optional"": true },
    { ""op"": ""dumpText"", ""timeout"": 5 },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["pull_page_names"] = @"
{
  ""name"": ""pull_page_names"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""what's on your mind|menu|pages"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""menu|more"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""pages"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""create new page|page"" }, ""timeout"": 25, ""optional"": true },
    { ""op"": ""dumpText"", ""timeout"": 5 },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["add_friend"] = @"
{
  ""name"": ""add_friend"",
  ""steps"": [
    { ""op"": ""openLink"", ""text"": ""fb://profile/{{uid}}"", ""timeout"": 15 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""add friend|message|follow"" }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^add friend$|add friend"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["confirm_friend"] = @"
{
  ""name"": ""confirm_friend"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""notifications|bell|alerts"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""friend request|confirm"" }, ""timeout"": 25, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^confirm$|confirm"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["join_group"] = @"
{
  ""name"": ""join_group"",
  ""steps"": [
    { ""op"": ""openLink"", ""text"": ""{{group}}"", ""timeout"": 15 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""join|joined|group"" }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^join group$|join group|^join$"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""request to join|send request|join"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["leave_group"] = @"
{
  ""name"": ""leave_group"",
  ""steps"": [
    { ""op"": ""openLink"", ""text"": ""{{group}}"", ""timeout"": 15 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""joined|leave|more"" }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^joined$|leave group|more"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""leave group"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^leave$|confirm"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["post_to_group"] = @"
{
  ""name"": ""post_to_group"",
  ""steps"": [
    { ""op"": ""openLink"", ""text"": ""{{group}}"", ""timeout"": 15 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""what's on your mind|write something|create post"" }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""tap"", ""find"": { ""regex"": ""what's on your mind|write something|create post"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""what's on your mind|say something"" }, ""text"": ""{{text}}"", ""timeout"": 12, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^post$|share"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["groups_suggestions"] = @"
{
  ""name"": ""groups_suggestions"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""search|menu|groups"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""search"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""search facebook"" }, ""text"": ""{{keyword}}"", ""timeout"": 12, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^groups$|groups"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""join|joined"", ""any"": true }, ""timeout"": 20, ""optional"": true },
    { ""op"": ""dumpText"", ""timeout"": 5 },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["share_post"] = @"
{
  ""name"": ""share_post"",
  ""steps"": [
    { ""op"": ""openLink"", ""text"": ""{{link}}"", ""timeout"": 15 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""share|comment|like"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""share"" }, ""timeout"": 10 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""share now|share to feed|share to a group|groups"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""log"", ""text"": ""shared {{link}}"" }
  ]
}",
                ["checkin_post"] = @"
{
  ""name"": ""checkin_post"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""what's on your mind|create post"" }, ""timeout"": 40 },
    { ""op"": ""tap"", ""find"": { ""regex"": ""what's on your mind|create post"" }, ""timeout"": 10 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""check in|location"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""search for a place|where are you"" }, ""text"": ""{{location}}"", ""timeout"": 12, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""{{location}}|^ok$|done"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^post$|share"" }, ""timeout"": 10 },
    { ""op"": ""verify"", ""find"": { ""regex"": ""posted|is now on facebook"" }, ""timeout"": 40, ""optional"": true }
  ]
}",
                ["create_story"] = @"
{
  ""name"": ""create_story"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""add to story|create story|your story"" }, ""timeout"": 40 },
    { ""op"": ""tap"", ""find"": { ""regex"": ""add to story|create story"" }, ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""gallery|photos|done|next"" }, ""timeout"": 25 },
    { ""op"": ""selectFiles"", ""text"": ""{{photo}}"", ""params"": { ""media"": ""photo"" } },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""done|next"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""link"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""add a link|enter link"" }, ""text"": ""{{link}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""done|save"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""share to story|^share$|your story"" }, ""timeout"": 10 },
    { ""op"": ""verify"", ""find"": { ""regex"": ""story shared|posted"" }, ""timeout"": 40, ""optional"": true }
  ]
}",
                ["create_page"] = @"
{
  ""name"": ""create_page"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""menu|pages|what's on your mind"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""menu|more"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""pages"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""create new page|create page"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""page name|name your page|get started"" }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""page name|name your page"" }, ""text"": ""{{name}}"", ""timeout"": 12, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""category|next|continue"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""category"" }, ""text"": ""{{category}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^create$|create page|next|done"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""verify"", ""find"": { ""regex"": ""your page|page created|welcome"" }, ""timeout"": 40, ""optional"": true }
  ]
}",
                ["set_account_info"] = @"
{
  ""name"": ""set_account_info"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""menu|profile|what's on your mind"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""menu|more"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""profile|account"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""edit profile|edit"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""{{field}}|about|info"" }, ""timeout"": 20, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""{{field}}"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""{{field}}|add|enter"" }, ""text"": ""{{value}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""save|done|ok"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["set_page_info"] = @"
{
  ""name"": ""set_page_info"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""menu|pages|what's on your mind"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""menu|more"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""pages"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""{{page}}|manage"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""edit|about|settings"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""{{field}}"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""{{field}}|add|enter"" }, ""text"": ""{{value}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""save|done|ok"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["professional_mode"] = @"
{
  ""name"": ""professional_mode"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""menu|settings|what's on your mind"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""menu|more"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""settings|settings & privacy"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""professional mode|professional"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""turn on|get started|turn off|disable"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["create_instagram"] = @"
{
  ""name"": ""create_instagram"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{igpackage}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""log in|sign up|continue with facebook"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""continue with facebook|log in with facebook"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""continue as|not now|allow"" }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""continue as|allow|not now"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""log"", ""text"": ""instagram linked (continue if account creation prompts appear)"" }
  ]
}",
                ["reply_inbox"] = @"
{
  ""name"": ""reply_inbox"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""inbox|messenger|chats|chat"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""inbox|messenger|chats"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""search|message|conversation"", ""any"": true }, ""timeout"": 20, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^messages$|search"", ""any"": true }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""swipe"", ""x"": 540, ""y"": 700, ""x2"": 540, ""y2"": 700, ""duration"": 300 },
    { ""op"": ""type"", ""find"": { ""regex"": ""message|write a reply|type a message"" }, ""text"": ""{{text}}"", ""timeout"": 12, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^send$|send"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["appeal"] = @"
{
  ""name"": ""appeal"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""appeal|disabled|lock|help"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""appeal|learn more|help"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""email|phone|continue|submit"" }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""email|contact email"" }, ""text"": ""{{email}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""date of birth|birthday|dob"" }, ""text"": ""{{dob}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^submit$|send|continue"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""verify"", ""find"": { ""regex"": ""received|submitted|we'll get back to you"" }, ""timeout"": 40, ""optional"": true }
  ]
}",
                ["unlock282"] = @"
{
  ""name"": ""unlock282"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""you can't use this feature|you cannot use this feature|temporarily blocked|unusual activity"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""confirm identity|get help|learn more|continue"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""code|email|phone|continue"" }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""enter code|code"" }, ""text"": ""{{otp}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^submit$|continue|confirm"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""verify"", ""find"": { ""regex"": ""unlocked|you're all set|done"" }, ""timeout"": 40, ""optional"": true }
  ]
}",
                ["verify_novery"] = @"
{
  ""name"": ""verify_novery"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""confirm your identity|verify|security|checkpoint"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""confirm|verify|get started|continue"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""{{channel}}|email|phone"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""enter code|code|send code"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""enter code|code"" }, ""text"": ""{{otp}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^submit$|continue|confirm"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""two-factor|2fa|security"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""turn on|enable|get started"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""log"", ""text"": ""novery verification finished"" }
  ]
}",
                ["reg_full"] = @"
{
  ""name"": ""reg_full"",
  ""steps"": [
    { ""op"": ""startAppFresh"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""create new account|sign up|log in"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""create new account|sign up"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""first name|last name|name"" }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""first name"" }, ""text"": ""{{first}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""last name"" }, ""text"": ""{{last}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""birthday|date of birth|day|month|year"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""birthday|date of birth|day"" }, ""text"": ""{{dob}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""{{gender}}|male|female|custom"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""mobile number|email|phone"" }, ""timeout"": 25, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""mobile number|email|phone"" }, ""text"": ""{{contact}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""new password|password"" }, ""text"": ""{{password}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^sign up$|sign up|register|continue"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""enter code|confirmation code|code"" }, ""timeout"": 45, ""optional"": true },
    { ""op"": ""log"", ""text"": ""reg_full: waiting for OTP confirmation"" }
  ]
}",
                ["reg_full_confirm"] = @"
{
  ""name"": ""reg_full_confirm"",
  ""steps"": [
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""enter code|confirmation code|code"" }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""enter code|confirmation code|code"" }, ""text"": ""{{otp}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^submit$|continue|confirm|done"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""verify"", ""find"": { ""regex"": ""what's on your mind|welcome|news feed"" }, ""timeout"": 60, ""optional"": true }
  ]
}",
                ["page_dashboard"] = @"
{
  ""name"": ""page_dashboard"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""menu|pages|what's on your mind"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""menu|more"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""pages"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""{{page}}|manage"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""professional dashboard|dashboard|insights"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""followers|reach|monetization|insights|overview"", ""any"": true }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""dumpText"", ""timeout"": 5 },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["delete_all_posts"] = @"
{
  ""name"": ""delete_all_posts"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""menu|pages|what's on your mind"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""menu|more"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""pages"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""{{page}}|manage"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""activity log|posts|manage posts"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^posts$|activity log"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""•••|more|menu"", ""exact"": false }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""delete"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^delete$|move to trash|confirm"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""log"", ""text"": ""delete_all_posts: one post deleted (repeat the flow for more)"" }
  ]
}",
                ["delete_page"] = @"
{
  ""name"": ""delete_page"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""menu|pages|what's on your mind"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""menu|more"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""pages"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""{{page}}|manage"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""settings|more|about"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""delete page|remove page"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^delete$|delete page|confirm"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""password"" }, ""text"": ""{{password}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^delete$|submit|confirm"" }, ""timeout"": 8, ""optional"": true }
  ]
}",
                ["auto_delete_copyright"] = @"
{
  ""name"": ""auto_delete_copyright"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""support inbox|inbox|menu"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""support inbox|inbox"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""copyright|rights|flag|violation"", ""any"": true }, ""timeout"": 25, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""copyright|violation"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""delete|remove"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^delete$|confirm|continue"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""log"", ""text"": ""auto_delete_copyright finished"" }
  ]
}",
                ["watch_live"] = @"
{
  ""name"": ""watch_live"",
  ""steps"": [
    { ""op"": ""openLink"", ""text"": ""{{link}}"", ""timeout"": 15 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""live|watching"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""sleep"", ""timeout"": 45 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^like$|like"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["view_story"] = @"
{
  ""name"": ""view_story"",
  ""steps"": [
    { ""op"": ""openLink"", ""text"": ""{{link}}"", ""timeout"": 15 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""story|more|reply"", ""any"": true }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""sleep"", ""timeout"": 20 },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["delete_last_comment"] = @"
{
  ""name"": ""delete_last_comment"",
  ""steps"": [
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""comment"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""•••|more|menu"", ""exact"": false }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""delete"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^delete$|confirm"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["check_notifications"] = @"
{
  ""name"": ""check_notifications"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""notifications|bell|alerts|what's on your mind"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""notifications|bell|alerts"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""friend request|comment|like|notification"", ""any"": true }, ""timeout"": 20, ""optional"": true },
    { ""op"": ""dumpText"", ""timeout"": 5 },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["reviews"] = @"
{
  ""name"": ""reviews"",
  ""steps"": [
    { ""op"": ""openLink"", ""text"": ""{{link}}"", ""timeout"": 15 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""reviews|recommendation|rate"" }, ""timeout"": 40, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""reviews|recommend"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""recommend|stars|rate"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""type"", ""find"": { ""regex"": ""write a review|add a review|what did you think"" }, ""text"": ""{{text}}"", ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""^post$|submit|share"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["check_primary_location"] = @"
{
  ""name"": ""check_primary_location"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""waitFor"", ""find"": { ""regex"": ""menu|profile|what's on your mind"" }, ""timeout"": 40 },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""menu|more"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""profile|account"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""about|see your about info"" }, ""timeout"": 10, ""optional"": true },
    { ""op"": ""tapIf"", ""find"": { ""regex"": ""places|lives in|hometown|location"" }, ""timeout"": 8, ""optional"": true },
    { ""op"": ""dumpText"", ""timeout"": 5 },
    { ""op"": ""back"", ""timeout"": 5 }
  ]
}",
                ["manual_login"] = @"
{
  ""name"": ""manual_login"",
  ""steps"": [
    { ""op"": ""startAppFresh"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""log"", ""text"": ""Please log in manually in the emulator window. The tool will capture the session afterwards."" },
    { ""op"": ""sleep"", ""timeout"": 240 },
    { ""op"": ""verify"", ""find"": { ""regex"": ""what's on your mind|news feed|home"", ""any"": true }, ""timeout"": 30, ""optional"": true },
    { ""op"": ""dumpText"", ""timeout"": 5 }
  ]
}",
                ["backup_profile"] = @"
{
  ""name"": ""backup_profile"",
  ""steps"": [
    { ""op"": ""startApp"", ""text"": ""{{package}}"", ""timeout"": 10 },
    { ""op"": ""pullAppData"", ""text"": ""{{package}}"", ""params"": { ""dest"": ""{{dest}}"" }, ""timeout"": 120 },
    { ""op"": ""log"", ""text"": ""profile data backed up to {{dest}}"" }
  ]
}"
            };
            return map;
        }
    }
}
