using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;
/*
struct info
{
    public string item;
    public int num;
    public string state;
}
*/

namespace myFUNC
{
    public class order
    {
        public string item;
        public int num;
        public string state;

    }
    

    
    [JsonObject(MemberSerialization.OptIn)] 
    public class Counter
    {
        [JsonProperty("stock")]
        public Dictionary<string, int> stock { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, float> unitprice { get; set; } = new Dictionary<string, float>();
        public Dictionary<string, float> tax { get; set; } = new Dictionary<string, float>();

        //get item
        public void add1(string item,int num)
        {
            this.stock.Add(item, num);
        }
        //set the price
        public void add2(string item, float price)
        {
            this.unitprice.Add(item, price);
        }
        //set the tax
        public void add3(string item, float price)
        {
            this.tax.Add(item, price);
        }


        //solditem
        public void Minus(string item,int num)
        {
            this.stock[item] -= num;
        }

        //check avaliablity
        public int Check(string item,int num)
        {
            if(this.stock[item] >= num)
            {
                return -1; //have enough item
            }
            else
            {
                return stock[item];//return the number of the item
            }
        }

        public Task<float> GetPrice(string item)
        {
            return Task.FromResult <float>(unitprice[item]);
        }

        public Task<float> GetTax(string item)// have to be tasks?
        {
            return Task.FromResult<float>(tax[item]);
        }


        [FunctionName(nameof(Counter))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
        => ctx.DispatchAsync<Counter>();
    }


    public static class myFUNC

    {

        [FunctionName("myFUNC")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            [DurableClient] IDurableEntityClient client,
            string entityKey)
        {

            //get item, number of item needed, delivery state for tax calculation
            order CustomerOrder = context.GetInput<order>();
            int avaliablity = await context.CallActivityAsync<int>("myFUNC_CheckStock",(CustomerOrder.item,CustomerOrder.num), entityKey);
            if (avaliablity == -1){ //there is enough number of items
                float price = await context.CallActivityAsync<float>("myFUNC_PriceCalculator", (CustomerOrder.item, CustomerOrder.num, CustomerOrder.state),entityKey);
                
                //SMS Activity FUNC
            }
            else
            {
                
                //SMS API FUNC
            }

            
        }

        
        [FunctionName("myFUNC_CheckStock")]
        public static async Task<int> Checkstock ([ActivityTrigger] IDurableActivityContext inputs,
            [DurableClient] IDurableEntityClient client,
            string EntityKey)

        { 
            (string item, int num) itemInfo = inputs.GetInput<(string, int)>();
            var entityId = new EntityId("Counter", EntityKey);
            int ans = await client.SignalEntityAsync<int>(entityId, "Check", (itemInfo.item, itemInfo.num));
            if (ans == -1)
            {
                await client.SignalEntityAsync(entityId,"Minus",(itemInfo.item,itemInfo.num));
                return -1;
            }
            else
            {
                return ans;
            }
        }
        [FunctionName("myFUNC_PriceCalculator")]
        public static async Task<float> pricecalculator([ActivityTrigger] IDurableActivityContext inputs,
            [DurableClient] IDurableEntityClient client,
            string EntityKey)
        {
            (string item, int num, string state) itemInfo = inputs.GetInput<(string, int, string)>();
            var entityId = new EntityId("Counter", EntityKey);
            float unitprice = await client.SignalEntityAsync<float>(entityId,"GetPrice",itemInfo.item);
            float tax = await client.SignalEntityAsync<float>(entityId, "GetTax", itemInfo.item);
            float price = unitprice * itemInfo.num * (1 + tax);

            return price;

        }


        //[FunctionName("myFUNC_SMS")]


        [FunctionName("myFUNC_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableEntityClient entityClient,
            string entityKey
            //copy from line 168
            )
        {
            // get the entitykey from the req input
            //string entityKey = req.Content.ReadAsStringAsync<string>(); ? Can I get that from req? 
            //create the new entity
            var entityID = new EntityId("Counter", entityKey);
            //initialze the dictionary stock
            await entityClient.SignalEntityAsync (entityID, "add1", ("Tshirt",100));
            await entityClient.SignalEntityAsync(entityID, "add1", ("hat",100));
            await entityClient.SignalEntityAsync(entityID, "add1", ("pants",1));
            await entityClient.SignalEntityAsync(entityID, "add2", ("Tshirt", 19.999));
            await entityClient.SignalEntityAsync(entityID, "add2", ("hat", 9.999));
            await entityClient.SignalEntityAsync(entityID, "add2", ("pants", 29.999));
            await entityClient.SignalEntityAsync(entityID, "add3", ("WA", 0.08));
            await entityClient.SignalEntityAsync(entityID, "add3", ("CA", 0.10));
            await entityClient.SignalEntityAsync(entityID, "add3", ("VA", 0.06));
            // why have to await?

            return req.CreateResponse(System.Net.HttpStatusCode.Accepted); // return what  to link the second http
            // copy from last case
        }


        [FunctionName("myFUNC_SalesStart")]
        public static async Task<HttpResponseMessage> SaleStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log,
            string entityId)
        {
            // Function input comes from the request content.
            order CustomerOrder = await req.Content.ReadAsAsync<order>();
            string instanceId = await starter.StartNewAsync("myFUNC",null,CustomerOrder,entityId);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        
        //route  what for? official website
        /*[FunctionName("DeleteCounter")]
        public static async Task<HttpResponseMessage> DeleteCounter(
    [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "Counter/{entityKey}")] HttpRequestMessage req,
    [DurableClient] IDurableEntityClient client,
    string entityKey)
        {
            var entityId = new EntityId("Counter", entityKey);
            await client.SignalEntityAsync(entityId, "Delete");
            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        */
    }
}
