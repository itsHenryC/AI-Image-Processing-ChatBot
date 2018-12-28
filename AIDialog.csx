using System;
using System.Configuration;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;

// For more information about this template visit http://aka.ms/azurebots-csharp-basic
[Serializable]
public class AIDialog : IDialog<object>
{
    private string lastImageUrl;

    public Task StartAsync(IDialogContext context)
    {
        try
        {
            context.Wait(MessageReceivedAsync);
        }
        catch (OperationCanceledException error)
        {
            return Task.FromCanceled(error.CancellationToken);
        }
        catch (Exception error)
        {
            return Task.FromException(error);
        }

        return Task.CompletedTask;
    }

    public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
    {
        var activity = await argument;
        if (activity.Attachments.Any())
        {
            lastImageUrl = activity.Attachments[0].ContentUrl;
            await context.PostAsync("Picture received!");
        }

        if (string.IsNullOrEmpty(lastImageUrl))
        {
            await context.PostAsync("Please send an image!");
        }
        else{
            ShowOptions(context);
        }
    }

    private void ShowOptions(IDialogContext context) {
        PromptDialog.Choice<string>(
                context,
                AfterChoiceReceivedAsync,
                new []{"Describe the picture", "Count the number of people", "Who's in the picture", "Forget this picture"},
                "What do you want to know about this image?",
                "Ops, I didn't get that...",
                5,
                promptStyle: PromptStyle.Auto);
    }

    public virtual async Task AfterChoiceReceivedAsync(IDialogContext context, IAwaitable<string> argument)
    {
        var response = await argument;

        switch (response)
        {
            case "Describe the picture":
                var (description, tags) = await InvokeComputerVisionAsync(lastImageUrl);
                await context.PostAsync($"I can see {description}, some words I would use to describe it are {tags}");
                break;
            case "Count the number of people":
                var peoples = await InvokeFaceApiAsync(lastImageUrl);
                int num = peoples.Count();
                if(num == 0){
                    await context.PostAsync("I don't think there is anyone in this picture");
                }
                else if (num == 1){
                    await context.PostAsync("I think there is one person in the picture");
                }
                else if (num > 1){
                    await context.PostAsync($"I think there are {num.ToString()} people.");
                }
                break;
            case "Who's in the picture":
                var people = await InvokeFaceApiAsync(lastImageUrl);
                var message = string.Join(", ", people.Select(s => $"a {s.Age} years old {s.Gender}{(s.Smile > .5 ? " smiling" : "")}"));
                if (message == ""){
                    await context.PostAsync($"I don't think there are any people in this picture.");
                }
                else{
                    await context.PostAsync($"I think there is {message}");
                }
                break;
            case "Forget this picture":
                lastImageUrl = null;
                await context.PostAsync($"Image? What Image?");
                return;
            default:
                await context.PostAsync("Invalid choice.");
                break;
        }

        ShowOptions(context);
    }

    public async Task<(string description,string tags)> InvokeComputerVisionAsync(string imageUrl)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", ConfigurationManager.AppSettings["VisionApiKey"]);
            var url = "https://canadacentral.api.cognitive.microsoft.com/vision/v2.0/analyze?visualFeatures=Categories,Description&language=en";

            var response = await client.PostAsync(new Uri(url), new StringContent("{\"url\":\"" + imageUrl + "\"}", System.Text.Encoding.UTF8, "application/json"));

            var json = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            return (json.description.captions[0].text, string.Join(", ", json.description.tags));
        }
    }

        public async Task<IEnumerable<FaceAttributes>> InvokeFaceApiAsync(string imageUrl)
        {        
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", ConfigurationManager.AppSettings["FaceApiKey"]);
                
                var url = "https://canadacentral.api.cognitive.microsoft.com/face/v1.0/detect?returnFaceAttributes=age,gender,smile";
                
                var response = await client.PostAsync(new Uri(url), new StringContent("{\"url\":\"" + imageUrl + "\"}", System.Text.Encoding.UTF8, "application/json"));

                var json = JsonConvert.DeserializeObject<List<Face>>(await response.Content.ReadAsStringAsync());
                
                return json.Select(s => s.FaceAttributes);
            }
        }

        public class Face 
        {
            public FaceAttributes FaceAttributes { get; set; }
        }

        public class FaceAttributes
        {
            public double Age { get; set; }
            public double Smile { get; set; }
            public string Gender { get; set; }
        }
}
