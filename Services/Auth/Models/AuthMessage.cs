namespace SkillForge.Services.Auth.Models
{
    public enum AuthMessage
    {
        //status for Register
        None,
        EmptyFields,
        PassNotMatch,
        EmailExist,
        EmailRegisteredAsStudent,
        EmailRegisteredAsInstructor,
        VerifyEmail,
        EmailVerified,
        EmailNotVerified,
        EmailSent,
        EmailNotSent,
        RegisterSuccess,
        RegisterFailed,

        //Status for Login
        LoginSuccess,
        LoginFailed,
        WrongPassword,
        NewUser,

        //OTP
        OtpSent,
        OtpVerified,
        OtpExpired,
        InvalidOtp,

        //Password Reset
        PasswordResetSuccess,
        PasswordResetFailed,
        LinkExpired
    }
}
