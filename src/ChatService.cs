using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace BouncyIcons {
    public sealed class ChatSettings {
        public string Endpoint="",Model="",ProtectedKey="",Persona="你是一个温柔、活泼的桌面小伙伴。用简短自然的中文聊天，不声称能操作用户电脑。";
        public static ChatSettings Load(){string path=Path.Combine(Program.DataDir,"chat-settings.json");return File.Exists(path)?new JavaScriptSerializer().Deserialize<ChatSettings>(File.ReadAllText(path))??new ChatSettings():new ChatSettings();}
        public string GetKey(){return String.IsNullOrEmpty(ProtectedKey)?"":Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedKey),null,DataProtectionScope.CurrentUser));}
        public void SetKey(string key){ProtectedKey=String.IsNullOrWhiteSpace(key)?"":Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()),null,DataProtectionScope.CurrentUser));}
        public void Save(){Validate();SharedData.AtomicWrite(Path.Combine(Program.DataDir,"chat-settings.json"),new JavaScriptSerializer().Serialize(this));}
        public Uri Validate(){
            Uri uri;if(!Uri.TryCreate(Endpoint,UriKind.Absolute,out uri)||!(uri.Scheme=="https"||(uri.Scheme=="http"&&uri.IsLoopback))||!String.IsNullOrEmpty(uri.UserInfo)||!String.IsNullOrEmpty(uri.Fragment)||!String.IsNullOrEmpty(uri.Query))throw new ArgumentException("填写完整 HTTPS 聊天接口地址；本机服务可用 http://localhost。地址中不要带密钥或查询参数。");
            if(String.IsNullOrWhiteSpace(Model))throw new ArgumentException("请填写服务商提供的模型名称。");return uri;
        }
    }
    public sealed class ChatMessage {
        public string role,content;
        public ChatMessage(){}
        public ChatMessage(string role,string content){this.role=role;this.content=content;}
    }
    public sealed class ChatReply {public ChatChoice[] choices;}
    public sealed class ChatChoice {public ChatMessage message;}
    public static class ChatService {
        // Injectable handler makes transport, failures and cancellation testable without a real provider.
        public static async Task<string> SendAsync(ChatSettings settings,IList<ChatMessage> history,CancellationToken cancellation,HttpMessageHandler handler=null){
            Uri uri=settings.Validate();string key=settings.GetKey();
            var messages=new List<ChatMessage>{new ChatMessage("system",settings.Persona??"")};
            foreach(var message in history)messages.Add(message);
            string body=new JavaScriptSerializer().Serialize(new {model=settings.Model,messages=messages,stream=false,max_tokens=512});
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            using(var client=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false},true))
            using(var request=new HttpRequestMessage(HttpMethod.Post,uri)){
                client.Timeout=TimeSpan.FromSeconds(45);client.MaxResponseContentBufferSize=1024*1024;
                request.Content=new StringContent(body,Encoding.UTF8,"application/json");
                if(!String.IsNullOrEmpty(key))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
                using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseContentRead,cancellation).ConfigureAwait(false)){
                    if(!response.IsSuccessStatusCode)throw new InvalidOperationException("API 返回 HTTP "+(int)response.StatusCode+"。请检查地址、模型、密钥或服务额度。");
                    string text=await response.Content.ReadAsStringAsync().ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
                    ChatReply reply;try{reply=new JavaScriptSerializer{MaxJsonLength=1024*1024}.Deserialize<ChatReply>(text);}catch{throw new InvalidOperationException("服务返回的内容不是兼容的聊天 JSON。");}
                    if(reply==null||reply.choices==null||reply.choices.Length==0||reply.choices[0]==null||reply.choices[0].message==null||String.IsNullOrWhiteSpace(reply.choices[0].message.content))throw new InvalidOperationException("API 未返回文字回复；请确认接口支持 Chat Completions。");
                    string content=reply.choices[0].message.content.Trim();return content.Length>8000?content.Substring(0,8000):content;
                }
            }
        }
    }
}
