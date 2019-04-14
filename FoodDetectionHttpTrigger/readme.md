# FoodDetectionHttpTrigger - C<span>#</span>

The `FoodDetectionHttpTrigger` executes the included function run.csx, when an HTTP Post Request comes in.

## Requirements

* Azure Blob Storage
* Azure Custom Vision Service

Set the following application settings variables:
* "BlobFoodDetectionImagesConnectionString"
* "CustomVisionEndpointWestEuropeUrl"
* "CustomVisionProjectName"
* "CustomVisionIterationName"
* "CustomVisionPredictionKey"
* "CustomVisionTrainingKey"
* "saveImageInBlob" as "true" or "false"
* "saveImageInCustomVisionService" as "true" or "false"

## How it works

When you call the function, be sure you checkout which security rules you apply. If you're using an apikey, you'll need to include that in your request.

Send with your post request an image. This filestream will be optionally uploaded to a blob storage, which generates a corresponding url.

After that, the custom vision service gets triggered with the generated url or the filestream and returns detected objects. These will be collected and resend as the HTTP response.