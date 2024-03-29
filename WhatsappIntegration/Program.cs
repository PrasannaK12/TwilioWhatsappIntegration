﻿using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using iTextSharp.text.pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using WhatsappIntegration.caching;

class Program
{
    private static IConfiguration Configuration { get; set; }
    private static IFeatureManager FeatureManager { get; set; }
    static void Main(string[] args)
    {
        InitializeConfiguration();
        //URL from we are retrieving the PDF in Bytes, From Vendor side.
        string url = Configuration["vendorURL"]; ;
        //Calling the URL.
        var dummyPdf = MakeGetRequest(url);
        //we are protecting PDF with dummy password, later you can configure it with your re
        var protectedPdf = ProtectPdfWithPassword(dummyPdf, "abcd1234");
        //we can configure those values in appsetting and local setting.
        var connectionString = Configuration["SAconnectionstring"];
        //connectionString = GetSecret("https://pkbadshah.vault.azure.net/", "SAconnectionstring");
        string containerName = "prasann";
        string blobName = "dummy_invoice_pk1.pdf";
        int maxAccessCount = 3;
        TimeSpan expirationTime = TimeSpan.FromMinutes(1); // 12 houss
        //uploading the protected PDF to blob
        UploadToBlobStorage(connectionString, containerName, blobName, protectedPdf);
        //generating the SAS with time period. after the time period it will not accessible.
        string sasToken = GenerateSasToken(connectionString, containerName, blobName, maxAccessCount, expirationTime);
        //integration with Twilio. to send PDF and message to User.
        bool isWhatsappEnabled = FeatureManager.IsEnabledAsync("isWhatsappEnabled").Result;
        if (isWhatsappEnabled)
        {
            WhatsappIntegration(sasToken);
        }
        else
        {
            Console.WriteLine("Skip Whatsapp call");
        }  
    }
    private static void InitializeConfiguration()
    {
        var builder = new ConfigurationBuilder();

        // Add Azure App Configuration
        // Assuming you have stored the connection string to Azure App Configuration in Key Vault
        string appConfigConnectionString = "Endpoint=https://appconfig-pk.azconfig.io;Id=nwcL;Secret=2/FBbhSF1KDW/gmCcG0cYL2u20vwjD21WokPOMnDPSU=";
        builder.AddAzureAppConfiguration(Options => Options.Connect(appConfigConnectionString).UseFeatureFlags());

        Configuration = builder.Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration);
        services.AddFeatureManagement();
        var serviceProvider = services.BuildServiceProvider();

        // Get the feature manager
        FeatureManager = serviceProvider.GetRequiredService<IFeatureManager>();
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
        var accountSid = rediscache.getValue("AccountSid");
        if (accountSid == null)
        {
            accountSid = GetSecret("https://pkbadshah.vault.azure.net/", "AccountSid");
            rediscache.setValue("AccountSid", accountSid);
        }
        var authToken = rediscache.getValue("TwilioAuthToken");
        if (authToken == null)
        {
            authToken = GetSecret("https://pkbadshah.vault.azure.net/", "TwilioAuthToken");
            rediscache.setValue("TwilioAuthToken", authToken);
        }
        TwilioClient.Init(accountSid, authToken);

        try
        {
            var messageOptions = new CreateMessageOptions(
              new PhoneNumber("whatsapp:+9181495*****"));
            messageOptions.From = new PhoneNumber("whatsapp:+14155238886");
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
    public static byte[] MakeGetRequest(string url)
    {
        try
        {
            using (HttpClient httpClient = new HttpClient())
            {
                HttpResponseMessage response = httpClient.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    byte[] content = response.Content.ReadAsByteArrayAsync().Result;
                    return content;
                }
                else
                {
                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            throw;
        }
    }
    public static byte[] ProtectPdfWithPassword(byte[] pdfContent, string password)
    {
        using (MemoryStream inputMemoryStream = new MemoryStream(pdfContent))
        using (MemoryStream outputMemoryStream = new MemoryStream())
        {
            PdfReader pdfReader = new PdfReader(inputMemoryStream);
            PdfEncryptor.Encrypt(pdfReader, outputMemoryStream, true, password, password, PdfWriter.ALLOW_PRINTING);

            return outputMemoryStream.ToArray();
        }
    }
    public static void UploadToBlobStorage(string connectionString, string containerName, string blobName, byte[] pdfData)
    {
        // Create a BlobServiceClient object which will be used to create a container client
        BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

        // Create the container and return a container client object
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        // Get a reference to a blob
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        // Open a new memory stream for the pdf data
        using (MemoryStream stream = new MemoryStream(pdfData))
        {
            // Upload the PDF data to the blob and overwrite if already exists.
            blobClient.Upload(stream, overwrite: true);
        }
    }
    public static string GetSecret(string keyVaultUrl, string secretName)
    {
        var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

        KeyVaultSecret secret = client.GetSecret(secretName);

        return secret.Value;
    }
}
