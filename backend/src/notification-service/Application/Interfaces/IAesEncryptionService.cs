namespace NotificationService.Application.Interfaces;

public interface IAesEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
