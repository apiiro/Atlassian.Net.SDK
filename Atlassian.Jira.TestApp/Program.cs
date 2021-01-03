using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using PanoramicData.ConsoleExtensions;

namespace Atlassian.Jira.TestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            MyPatches.DoPatching();

            Console.WriteLine("Please enter server url, then username, then password");
            var url = Console.ReadLine();
            var username = Console.ReadLine();
            var password = ConsolePlus.ReadPassword();

            var jiraClient = Jira.CreateRestClient(url, username, password);
            Console.WriteLine("");

            Console.WriteLine("First attempt");
            await AttemptAsync(jiraClient);

            Console.WriteLine("Second attempt");
            jiraClient.RestClient.RestSharpClient.RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true;
            await AttemptAsync(jiraClient);

            var originalSecurityProtocol = ServicePointManager.SecurityProtocol;
            Console.WriteLine($"Original security protocol: {originalSecurityProtocol}");

            Console.WriteLine("Third attempt");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            await AttemptAsync(jiraClient);

            Console.WriteLine("Fourth attempt");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;
            await AttemptAsync(jiraClient);
        }

        private static async Task AttemptAsync(Jira jiraClient)
        {
            try
            {
                var projectsCount = (await jiraClient.Projects.GetProjectsAsync()).Count();
                Console.WriteLine($"Projects count: {projectsCount}");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }

    public static class MyPatches
    {
        public static void DoPatching()
        {
            var harmony = new Harmony("HttpValidationHelpers");
            var methodInfo = Assembly.GetAssembly(typeof(WebHeaderCollection)).GetTypes().FirstOrDefault(type => type.Name == "HttpValidationHelpers").GetMethod("CheckBadHeaderValueChars");
            harmony.Patch(
                methodInfo,
                prefix: new HarmonyMethod(GetMethod(nameof(Prefix))),
                finalizer: new HarmonyMethod(GetMethod(nameof(ExceptionSilencingFinalizer)))
            );
        }

        private static MethodInfo GetMethod(string name) => typeof(MyPatches).GetMethod(name, BindingFlags.Static | BindingFlags.Public);

        public static void Prefix(ref string value)
        {
            var stringBuilder = new StringBuilder(value);
            stringBuilder[0] = (char) 127;
            value = stringBuilder.ToString();
        }

        public static Exception ExceptionSilencingFinalizer(Exception __exception)
            => null;
    }
}