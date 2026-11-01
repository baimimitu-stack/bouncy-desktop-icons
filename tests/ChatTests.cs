using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BouncyIcons;

class ChatTests {
    sealed class Fake:HttpMessageHandler {
        public string Body,Token;
        public HttpStatusCode Status=HttpStatusCode.OK;
        public string Reply="{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"你好呀\"}}]}";
        public bool Wait;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation){
            Body=await request.Content.ReadAsStringAsync();Token=request.Headers.Authorization==null?"":request.Headers.Authorization.Parameter;
            if(Wait)await Task.Delay(10000,cancellation);
            return new HttpResponseMessage(Status){Content=new StringContent(Reply)};
        }
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Main(string[] args){try{Run(args).GetAwaiter().GetResult();Console.WriteLine("PASS: encrypted key; request shape; Unicode reply; status errors; malformed reply; cancellation; endpoint validation (mock transport, no paid API calls)");}catch(Exception e){Console.WriteLine("FAIL: "+e.Message);Environment.ExitCode=1;}}
    static async Task Run(string[] args){
        Program.DataDir=Path.Combine(Path.GetFullPath(args[0]),"smoke-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Program.DataDir);
        var cfg=new ChatSettings{Endpoint="https://example.invalid/v1/chat/completions",Model="test-model",Persona="测试人设"};cfg.SetKey("unit-test-secret");cfg.Save();
        Check(!File.ReadAllText(Path.Combine(Program.DataDir,"chat-settings.json")).Contains("unit-test-secret"),"plaintext key on disk");
        cfg=ChatSettings.Load();Check(cfg.GetKey()=="unit-test-secret","DPAPI roundtrip");
        var history=new List<ChatMessage>{new ChatMessage("user","你好")};var fake=new Fake();
        string reply=await ChatService.SendAsync(cfg,history,CancellationToken.None,fake);
        Check(reply=="你好呀"&&fake.Token=="unit-test-secret"&&fake.Body.Contains("test-model")&&fake.Body.Contains("测试人设")&&!fake.Body.Contains("unit-test-secret"),"request or response");
        foreach(var status in new[]{HttpStatusCode.Unauthorized,HttpStatusCode.Redirect,(HttpStatusCode)429}){
            bool failed=false;try{await ChatService.SendAsync(cfg,history,CancellationToken.None,new Fake{Status=status});}catch(InvalidOperationException e){failed=e.Message.Contains(((int)status).ToString());}Check(failed,"HTTP failure handling");
        }
        foreach(string json in new[]{"not json","{}","{\"choices\":[]}","{\"choices\":[null]}"}){
            bool failed=false;try{await ChatService.SendAsync(cfg,history,CancellationToken.None,new Fake{Reply=json});}catch(InvalidOperationException){failed=true;}Check(failed,"malformed response");
        }
        using(var cancel=new CancellationTokenSource()){var task=ChatService.SendAsync(cfg,history,cancel.Token,new Fake{Wait=true});cancel.Cancel();bool stopped=false;try{await task;}catch(OperationCanceledException){stopped=true;}Check(stopped,"request cancellation");}
        cfg.Endpoint="http://example.invalid/chat/completions";bool rejected=false;try{cfg.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"remote plaintext transport");
        cfg.Endpoint="http://localhost:1234/v1/chat/completions";cfg.SetKey("");Check(cfg.Validate().IsLoopback&&cfg.GetKey()=="","local API without key");
    }
}
