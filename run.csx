#r "Newtonsoft.Json"
#load "AIDialog.csx"

using System;
using System.Net;
using System.Threading;
using Newtonsoft.Json;

using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

// Bot Storage: Register the optional private state storage for your bot. 

// (Default) For Azure Table Storage, set the following environment variables in your bot app:
// -UseTableStorageForConversationState set to 'true'
// -AzureWebJobsStorage set to your table connection string

// For CosmosDb, set the following environment variables in your bot app:
// -UseCosmosDbForConversationState set to 'true'
// -CosmosDbEndpoint set to your cosmos db endpoint
// -CosmosDbKey set to your cosmos db key

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    // Initialize the azure bot
    using (BotService.Initialize())
    {
        // Deserialize the incoming activity
        string jsonContent = await req.Content.ReadAsStringAsync();
        var activity = JsonConvert.DeserializeObject<Activity>(jsonContent);
        log.Info(jsonContent);
        // authenticate incoming request and add activity.ServiceUrl to MicrosoftAppCredentials.TrustedHostNames
        // if request is authenticated
        if (!await BotService.Authenticator.TryAuthenticateAsync(req, new [] {activity}, CancellationToken.None))
        {
            return BotAuthenticator.GenerateUnauthorizedResponse(req);
        }
        
        if (activity != null)
        {
            // one of these will have an interface and process it
            switch (activity.GetActivityType())
            {
                case ActivityTypes.Message:
                    await Conversation.SendAsync(activity, () => new AIDialog());
                    break;
                case ActivityTypes.ConversationUpdate:
                    IConversationUpdateActivity update = activity;
                    var client = new ConnectorClient(new Uri(activity.ServiceUrl));
                    if (update.MembersAdded != null && update.MembersAdded.Any())
                    {       
                        var newMembers = update.MembersAdded?.Where(t => t.Id != activity.Recipient.Id);
                        foreach (var newMember in newMembers)
                        {
                            var reply = activity.CreateReply();
                            reply.Text = "Welcome!";
                            await client.Conversations.ReplyToActivityAsync(reply);
                        }
                    }
                    break;
                case ActivityTypes.ContactRelationUpdate:
                case ActivityTypes.Typing:
                case ActivityTypes.DeleteUserData:
                case ActivityTypes.Ping:
                default:
                    log.Error($"Unknown activity type ignored: {activity.GetActivityType()}");
                    break;
            }
        }
        return req.CreateResponse(HttpStatusCode.Accepted);
    }   
}

