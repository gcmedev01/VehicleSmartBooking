namespace VehicleBooking.Web.Domain.Services
{
    public interface IPasswordHasher
    {
        PasswordHashResult Hash(string password, int? iterations = null);
        bool Verify(string password, byte[] hash, byte[] salt, int iterations, string? algo);
    }

    public sealed record PasswordHashResult(byte[] Hash, byte[] Salt, int Iterations, string Algo);
}
