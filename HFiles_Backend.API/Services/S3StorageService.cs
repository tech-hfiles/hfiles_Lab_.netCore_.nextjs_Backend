using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

public class S3StorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string? _bucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME");
    private readonly string? _cdnBaseUrl = Environment.GetEnvironmentVariable("AWS_CDN_BASE_URL");


    public S3StorageService()
    {
        var awsAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var awsSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        var awsRegion = Environment.GetEnvironmentVariable("AWS_REGION");
        var awsCredentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        _s3Client = new AmazonS3Client(awsCredentials, RegionEndpoint.GetBySystemName(awsRegion));
    }

    public async Task<string?> UploadFileToS3(string filePath, string key)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var contentType = GetContentType(Path.GetExtension(filePath));
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = fileStream,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(putRequest);

            return $"{_cdnBaseUrl}{key}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading file: {ex.Message}");
            return null;
        }
    }

    private static string GetContentType(string fileExtension)
    {
        return fileExtension.ToLower() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
