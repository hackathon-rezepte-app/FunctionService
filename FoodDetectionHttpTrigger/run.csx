#r "Microsoft.WindowsAzure.Storage"

using System;
using System.Configuration;
using System.Net;
using System.Text;
using Microsoft.Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training;

public static async Task<IActionResult> Run(HttpRequestMessage req, TraceWriter log)
{
    HttpStatusCode result;
    string contentType;
    double threshold = 0.0;
    List<PredictionItem> predictionItems = new List<PredictionItem>();

    result = HttpStatusCode.BadRequest;
    contentType = req.Content.Headers?.ContentType?.MediaType;

    var queryVals = req.RequestUri.ParseQueryString();
    var message = queryVals["threshold"];
    threshold = (message != null) ? Convert.ToDouble(message) : 0.6;
    log.Info($"{threshold}");
    bool isOctetStream = contentType == "application/octet-stream" ? true:true;
    if(isOctetStream)
    {
        Stream body;
        body = await req.Content.ReadAsStreamAsync();

        string name;

        name = Guid.NewGuid().ToString("n");

        bool sucess = await CreateBlob(name + ".jpg", body, log);
        string base_url = "https://fooddetectionfubd10.blob.core.windows.net/images/";
        predictionItems = await DetectObjectsFromUrl(base_url + name + ".jpg", threshold);

        result = HttpStatusCode.OK;
    }
    return new ObjectResult(predictionItems);
}

private async static Task<List<PredictionItem>> DetectObjectsFromUrl(string url, double threshold = 0.0)
{
    string endpointurl = Environment.GetEnvironmentVariable("CustomVisionEndpointWestEuropeUrl",EnvironmentVariableTarget.Process);
    string iterationName = Environment.GetEnvironmentVariable("CustomVisionIterationName",EnvironmentVariableTarget.Process);
    string predictionKey = Environment.GetEnvironmentVariable("CustomVisionPredictionKey",EnvironmentVariableTarget.Process);
    string trainingKey = Environment.GetEnvironmentVariable("CustomVisionTrainingKey",EnvironmentVariableTarget.Process);
    string projectName = Environment.GetEnvironmentVariable("CustomVisionProjectName",EnvironmentVariableTarget.Process);
    CustomVisionTrainingClient trainingApi = new CustomVisionTrainingClient()
    {
        ApiKey = trainingKey,
        Endpoint = endpointurl
    };
    CustomVisionPredictionClient endpoint = new CustomVisionPredictionClient()
    {
            ApiKey = predictionKey,
            Endpoint = endpointurl
    };
    Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models.Project project = null;
    Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models.Iteration iterationItem = null;

    foreach(Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models.Project tempproject in  trainingApi.GetProjects())
    {
        if(tempproject.Name == projectName)
        {
            project = tempproject;
        }
    }
    //if(project == null){return;}
    foreach(var iteration in trainingApi.GetIterations(project.Id))
    {
        if(iteration.Name == iterationName && iteration.PublishName != "")
        {
            iterationItem = iteration;
            break;
        }
    }
    //if(iterationItem == null) {return;}
    List<PredictionItem> predictionItems = new List<PredictionItem>();
    Console.WriteLine($"Making a prediction:{project.Name}");
    {
        ImagePrediction result = endpoint.DetectImageUrl(project.Id, iterationItem.PublishName, new ImageUrl(url));

        // Loop over each prediction and write out the results
        foreach (var c in result.Predictions)
        {
            if(c.Probability < threshold) { continue; }
            PredictionItem predictionItem = new PredictionItem{
                TagName = c.TagName,
                Propability = c.Probability,
                BoundingBox = new BoundingBox{
                    Top = c.BoundingBox.Top,
                    Left = c.BoundingBox.Left,
                    Width = c.BoundingBox.Width,
                    Height = c.BoundingBox.Height
                }
            };
            predictionItems.Add(predictionItem);
        }
    }
    return predictionItems;
}

internal class PredictionItem
{
    internal string TagName {get;set;}
    internal double Propability {get;set;}
    internal BoundingBox BoundingBox {get;set;}
}

internal class BoundingBox
{
    internal double Top{get;set;}
    internal double Left{get;set;}
    internal double Width{get;set;}
    internal double Height{get;set;}
}

private async static Task<bool> CreateBlob(string name, Stream stream, TraceWriter log)
{
    string accessKey;
    string accountName;
    string connectionString;
    CloudStorageAccount storageAccount;
    CloudBlobClient client;
    CloudBlobContainer container;
    CloudBlockBlob blob;

    connectionString = Environment.GetEnvironmentVariable("BlobFoodDetectionImagesConnectionString",EnvironmentVariableTarget.Process);
    storageAccount = CloudStorageAccount.Parse(connectionString);

    client = storageAccount.CreateCloudBlobClient();
    
    container = client.GetContainerReference("images");
   
    await container.CreateIfNotExistsAsync();
    
    blob = container.GetBlockBlobReference(name);
    blob.Properties.ContentType = "application/octet-stream";

    //using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(data)))
    //{
        await blob.UploadFromStreamAsync(stream);
    //}
    return true;
}