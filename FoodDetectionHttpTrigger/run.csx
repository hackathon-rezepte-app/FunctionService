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

public static async Task<IActionResult> Run(HttpRequestMessage req, ILogger log)
{
    string contentType;
    double threshold = 0.0;
    List<PredictionItem> predictionItems = new List<PredictionItem>();
    string base_url = "https://fooddetectionfubd10.blob.core.windows.net/images/";
    string envSaveImageInBlob = Environment.GetEnvironmentVariable("saveImageInBlob",EnvironmentVariableTarget.Process);
    bool saveImageInBlob = envSaveImageInBlob == "true";

    contentType = req.Content.Headers?.ContentType?.MediaType;

    var queryVals = req.RequestUri.ParseQueryString();
    var message = queryVals["threshold"];
    threshold = (message != null) ? Convert.ToDouble(message) : 0.6;
    log.LogInformation($"{threshold}");
    bool isOctetStream = contentType == "application/octet-stream" ? true:true;
    if(isOctetStream)
    {
        Stream bodyStream = await req.Content.ReadAsStreamAsync();
        
        if(saveImageInBlob)
        {
            string name = Guid.NewGuid().ToString("n");
            bool sucess = await CreateBlob(name + ".jpg", bodyStream, log);
            predictionItems = await DetectObjects(new ImageUrl(base_url + name + ".jpg"), log, threshold);
        }
        else
        {
            predictionItems = await DetectObjects(bodyStream, log, threshold);
        }
    }
    return new ObjectResult(predictionItems);
}

private async static Task<List<PredictionItem>> DetectObjects(object source, ILogger log, double threshold = 0.0)
{
    string endpointurl = Environment.GetEnvironmentVariable("CustomVisionEndpointWestEuropeUrl",EnvironmentVariableTarget.Process);
    string iterationName = Environment.GetEnvironmentVariable("CustomVisionIterationName",EnvironmentVariableTarget.Process);
    string predictionKey = Environment.GetEnvironmentVariable("CustomVisionPredictionKey",EnvironmentVariableTarget.Process);
    string trainingKey = Environment.GetEnvironmentVariable("CustomVisionTrainingKey",EnvironmentVariableTarget.Process);
    string projectName = Environment.GetEnvironmentVariable("CustomVisionProjectName",EnvironmentVariableTarget.Process);
    string envSaveImageInCustomVisionService = Environment.GetEnvironmentVariable("saveImageInCustomVisionService",EnvironmentVariableTarget.Process);
    bool saveImageInCustomVisionService = envSaveImageInCustomVisionService == "true";
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
    ImagePrediction result;

    if(typeof(Stream).IsAssignableFrom(source.GetType()))
    {
        if(saveImageInCustomVisionService)
            result = endpoint.DetectImage(project.Id, iterationItem.PublishName, (Stream)source);
        else 
            result = endpoint.DetectImageWithNoStore(project.Id, iterationItem.PublishName, (Stream)source);
    }
    else if(typeof(ImageUrl).Equals(source.GetType()))
    {
        if(saveImageInCustomVisionService)
            result = endpoint.DetectImageUrl(project.Id, iterationItem.PublishName, (ImageUrl)source);
        else
            result = endpoint.DetectImageUrlWithNoStore(project.Id, iterationItem.PublishName, (ImageUrl)source);
    }
    else
        throw new InvalidDataException();

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

private async static Task<bool> CreateBlob(string name, Stream stream, ILogger log)
{
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