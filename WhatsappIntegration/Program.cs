using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

class Program
{
    static void Main(string[] args)
    {
        string connectionString = "DtNtJGwDft/caNk85/JsyFgvcHxhHx+anV8+AStRP4tNg==;EndpointSuffix=core.windows.net";
        string containerName = "prasann";
        string blobName = "dummy_invoice.pdf";
        int maxAccessCount = 3;
        TimeSpan expirationTime = TimeSpan.FromMinutes(1); // 12 houss

        string sasToken = GenerateSasToken(connectionString, containerName, blobName, maxAccessCount, expirationTime);
        WhatsappIntegration(sasToken);
        Console.WriteLine($"SAS Token: {sasToken}");
    }

    static string GenerateSasToken(string connectionString, string containerName, string blobName, int maxAccessCount, TimeSpan expirationTime)
    {
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        // Generate the SAS token
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b", // "b" for blob, "c" for container
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.Add(expirationTime),
        };

        // Set the permissions for the SAS token
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        // Generate the SAS token directly
        var sasToken = blobClient.GenerateSasUri(sasBuilder).Query;

        // Manually add the maxUses parameter to the query string
        sasToken += $"&maxUses={maxAccessCount}";

        return blobClient.Uri + sasToken;
    }
    public static string WhatsappIntegration(string sasUrl)
    {
        var accountSid = "AC1cbb8e6b45fdea5"; // Replace with your Account SID
        var authToken = "8eb955ea316a76a9a4";   // Replace with your Auth Token
        TwilioClient.Init(accountSid, authToken);

        try
        {
            var messageOptions = new CreateMessageOptions(
              new PhoneNumber("whatsapp:+918149****")); // Replace with recipient's number
            messageOptions.From = new PhoneNumber("whatsapp:+14155238886"); // Your Twilio WhatsApp number
            messageOptions.Body = "Hi.";
            if (!string.IsNullOrEmpty(sasUrl))
            {
                messageOptions.MediaUrl = new List<Uri> { new Uri(sasUrl) };
            }

            var message = MessageResource.Create(messageOptions);
            return "Message sent successfully. SID: ";
        }
        catch (Exception ex)
        {
            return "Error sending message: " + ex.Message;
        }
    }

}
