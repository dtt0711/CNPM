namespace TourismHcm.Application.Abstractions.Providers;

public interface IPiiProtector
{
    string Encrypt(string plaintext);

    string? Decrypt(string? ciphertext);
}
