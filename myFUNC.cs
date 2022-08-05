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
    public class inventory
    {
        public Dictionary<string, int> stock;
        public Dictionary<string, double> unitprice;
        public Dictionary<string, double> tax;

    }

    public static class myFUNC

    {

        [FunctionName("myFUNC")]
        public static async Task<int> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {

            //get item, number of item needed, delivery state for tax calculation
            (string item, int num, string state) info = context.GetInput<(string, int,string)>();
            int avaliablity = await context.CallActivityAsync<int>("myFUNC_CheckStock",(info.item,info.num));
            if (avaliablity == -1){ //there is enough number of items
                double price = await context.CallActivityAsync<double>("myFUNC_PriceCalculator", (info.item,info.num,info.state));
                Console.WriteLine("Thanks! Your order is successfully processed and the total price is {0}", price);
                //SMS Activity FUNC
            }
            else
            {
                Console.WriteLine("Oops! Sorry that we only have {0} left of {1}, if you want please make orders again. ", avaliablity, info.item);
                //SMS API FUNC
            }

            return -1;
        }

        
        [FunctionName("myFUNC_CheckStock")]
        public static int checkstock ([ActivityTrigger] IDurableActivityContext inputs)
        {
            
            (string item, int num) itemInfo = inputs.GetInput<(string, int)>();
            //IDictionary<string, int> stock = new Dictionary<string, int>();
            inventory website = new inventory();
            if (website.stock[itemInfo.item] >= itemInfo.num)
            {
                website.stock[itemInfo.item] = website.stock[itemInfo.item] - itemInfo.num; // dont know if this works,but there is no error when debugging
                return -1;
            }
            else
            {
                return website.stock[itemInfo.item];
            }
        }
        [FunctionName("myFUNC_PriceCalculator")]
        public static double pricecalculator([ActivityTrigger] IDurableActivityContext inputs)
        {
            (string item, int num, string state) itemInfo = inputs.GetInput<(string, int, string)>();
            inventory website = new inventory();
            double price = (website.unitprice[itemInfo.item] + website.unitprice[itemInfo.item] * website.tax[itemInfo.state]) * itemInfo.num;
            return price;

        }

    
        //[FunctionName("myFUNC_SMS")]





        [FunctionName("myFUNC_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("myFUNC", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
