using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FarmReel.Core.Data;
using FarmReel.Core.Models;
using FarmReel.Core.Utils;

namespace FarmReel.Core.Services
{
    /// <summary>
    /// FarmReel-differentiator pipeline: bulk registration ("Reg Full"),
    /// novelty verification ("Verify Novery"), and error-282 unlock.
    /// These flows require Facebook UI automation (implemented in the Automation layer);
    /// this service manages state, OTP capture and account handoff.
    /// </summary>
    public class RegistrationService
    {
        private readonly AccountRepository _accounts;
        private readonly DeviceRepository _devices;
        private readonly SettingsService _settings;
        private readonly IFlowRunner _flows;
        private readonly EmailOtpService _emailOtp;
        private readonly SmsService _sms;
        private readonly CaptchaService _captcha;
        private readonly LicenseService _license;
        private readonly Random _rng = new Random();

        private static readonly string[] FirstNames =
        {
            "Sokha", "Dara", "Rithy", "Bopha", "Vichea", "Sreymom", "Kosal", "Chanthou",
            "Narith", "Davin", "Sothea", "Rady", "Malis", "Visal", "Channary", "Piseth",
            "Kimleng", "Sopheap", "Vuthy", "Mony", "Lina", "Serey", "Chenda", "Rathana"
        };

        private static readonly string[] LastNames =
        {
            "Chan", "Sok", "Kim", "Meas", "Chea", "Soun", "Ly", "Tep", "Phan", "Heng",
            "Lim", "Nop", "Ouk", "Pich", "Ros", "Sam", "Tang", "Ung", "Vong", "Yem"
        };

        public RegistrationService(AccountRepository accounts, DeviceRepository devices, SettingsService settings,
            IFlowRunner flows, EmailOtpService emailOtp, SmsService sms, CaptchaService captcha, LicenseService license)
        {
            _accounts = accounts;
            _devices = devices;
            _settings = settings;
            _flows = flows;
            _emailOtp = emailOtp;
            _sms = sms;
            _captcha = captcha;
            _license = license;
        }

        public string GenerateName() => FirstNames[_rng.Next(FirstNames.Length)] + " " + LastNames[_rng.Next(LastNames.Length)];

        /// <summary>Run bulk registration: for each device, run the reg flow, capture OTP, save the new account.</summary>
        public async Task<RegistrationResult> RegisterBulkAsync(IEnumerable<long> deviceIds, RegistrationOptions options)
        {
            if (!_license.CanUseRegFull)
                return new RegistrationResult { Success = false, Message = "Registration requires the 12-month plan (Reg Full)" };

            var result = new RegistrationResult { Success = true };
            foreach (var deviceId in deviceIds)
            {
                var device = _devices.Get(deviceId);
                if (device == null) continue;
                try
                {
                    var email = options.Email ?? "";
                    var phone = options.Phone;
                    var name = options.Name ?? GenerateName();

                    var flowResult = await _flows.RunActionAsync(device, null, "reg_full",
                        new Dictionary<string, string>
                        {
                            ["name"] = name,
                            ["email"] = email,
                            ["phone"] = phone ?? "",
                            ["dob"] = options.Dob ?? "",
                            ["gender"] = options.Gender ?? "male",
                            ["mail_otp"] = "true"
                        }).ConfigureAwait(false);

                    var otp = "";
                    if (!string.IsNullOrEmpty(email))
                    {
                        otp = await _emailOtp.GetCodeAsync(email, options.MailLogin, options.MailPassword,
                            options.MailProvider, options.OtpWaitSeconds).ConfigureAwait(false);
                    }
                    else if (!string.IsNullOrEmpty(phone))
                    {
                        // SMS flow uses the number rented by the reg flow
                        otp = await _sms.GetCodeAsync(flowResult.Data.GetValueOrDefault("activation_id"), 120)
                            .ConfigureAwait(false);
                        await _sms.ReleaseNumberAsync(flowResult.Data.GetValueOrDefault("activation_id")).ConfigureAwait(false);
                    }

                    var confirm = await _flows.RunActionAsync(device, null, "reg_full_confirm",
                        new Dictionary<string, string> { ["otp"] = otp }).ConfigureAwait(false);

                    var account = new Account
                    {
                        Email = email,
                        Phone = phone ?? "",
                        Name = name,
                        DateOfBirth = options.Dob ?? "",
                        DeviceId = device.Id,
                        DateCreated = DateTime.Now,
                        Status = confirm.Success ? AccountStatus.Live : AccountStatus.Checkpoint,
                        MailProvider = options.MailProvider,
                        Notes = "Registered via Reg Full"
                    };
                    if (!string.IsNullOrEmpty(options.Password))
                        account.PasswordEnc = CredentialVault.Protect(options.Password);
                    _accounts.Save(account);
                    result.Created.Add(account);
                    result.Log.Add($"Device {device.Name}: registered {email} ({name}) -> {(confirm.Success ? "confirmed" : "needs attention")}");
                }
                catch (Exception ex)
                {
                    result.Failures++;
                    result.Log.Add($"Device {device.Name}: FAILED - {ex.Message}");
                    Log.Error("RegFull", ex, device.Name);
                }
            }
            result.Message = string.Join("\n", result.Log);
            return result;
        }

        /// <summary>Novelty verification: confirm identity (email/phone) and enable 2FA to make the account look aged.</summary>
        public async Task<FlowResult> VerifyNoveryAsync(Account account, string channel)
        {
            if (!_license.CanUseNovery)
                return new FlowResult { Success = false, Message = "Verify Novery requires the 12-month plan" };
            var device = _devices.Get(account.DeviceId);
            var otp = "";
            if (channel == "email" && !string.IsNullOrEmpty(account.Email))
                otp = await _emailOtp.GetCodeAsync(account.Email, account.Email, GetPasswordSafe(account), account.MailProvider, 60)
                    .ConfigureAwait(false);
            return await _flows.RunActionAsync(device, account, "verify_novery",
                new Dictionary<string, string> { ["channel"] = channel, ["otp"] = otp }).ConfigureAwait(false);
        }

        private string GetPasswordSafe(Account a)
        {
            try { return CredentialVault.Unprotect(a.PasswordEnc); } catch { return ""; }
        }
    }

    public class RegistrationOptions
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string MailLogin { get; set; }
        public string MailPassword { get; set; }
        public string MailProvider { get; set; } = "gmail";
        public string Phone { get; set; }
        public string Dob { get; set; }
        public string Gender { get; set; } = "male";
        public string Password { get; set; }
        public int OtpWaitSeconds { get; set; } = 60;
    }

    public class RegistrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public List<Account> Created { get; set; } = new List<Account>();
        public List<string> Log { get; set; } = new List<string>();
        public int Failures { get; set; }
    }
}
