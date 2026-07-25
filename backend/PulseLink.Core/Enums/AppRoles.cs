namespace PulseLink.Core.Enums;

public static class AppRoles
{
    public const string Paramedic = "Paramedic";
    public const string HospitalStaff = "HospitalStaff";
    public const string Admin = "Admin";

    public static readonly string[] All = [Paramedic, HospitalStaff, Admin];
}
