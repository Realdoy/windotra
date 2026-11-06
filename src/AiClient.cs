using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WinCanvas {
 public class AiConfig {
  public string Provider="Auto",Endpoint="http://localhost:11434/api/chat",Model="",BrowserSite="Claude";
  public string Instructions="You are a helpful assistant inside WinDotra, a Windows desktop island. Be concise and practical. You can only see the user's screen when they share a screenshot with a message. You cannot read files or execute actions yourself. Do not claim you changed the user's computer.";
  public int MaxTokens=2048; public bool RememberChat=false,AllowActions=true,AutoScreen=true,AlwaysScreen=false;
  // Keyless engines shown in Studio. Claude/OpenAI/Compatible remain only as protocol code for local OpenAI-style servers and tests.
  public static string[] Providers={"Auto","Ollama","Claude Code","Codex","Claude app","ChatGPT app","Browser","Compatible","Claude","OpenAI"};
  public static string[] Keyless={"Auto","Ollama","Claude Code","Codex","Claude app","ChatGPT app","Browser"};
  // A screenshot to send with the next message only; never saved with the settings.
  [ScriptIgnore] public string ImagePath;
  public bool NeedsModel {get{return Provider=="Compatible"||Provider=="Claude"||Provider=="OpenAI";}}
  public void Validate(bool requireModel) {
   if(!Providers.Contains(Provider)||Model==null||Model.Length>150||Instructions==null||Instructions.Length>6000||MaxTokens<128||MaxTokens>8192)throw new InvalidDataException("Invalid AI settings.");
   if(BrowserSite!="Claude"&&BrowserSite!="ChatGPT")throw new InvalidDataException("Invalid browser assistant.");
   if(requireModel&&NeedsModel&&String.IsNullOrWhiteSpace(Model))throw new InvalidDataException("Choose a model ID in Studio → AI assistant first.");
   Uri uri;
   if(Endpoint==null||Endpoint.Length>2048||!Uri.TryCreate(Endpoint,UriKind.Absolute,out uri)||!String.IsNullOrEmpty(uri.UserInfo)||!String.IsNullOrEmpty(uri.Query)||!String.IsNullOrEmpty(uri.Fragment))throw new InvalidDataException("Use a complete API URL without credentials, query, or fragment.");
   if(uri.Scheme!="https" && !(uri.Scheme=="http"&&uri.IsLoopback))throw new InvalidDataException("Use HTTPS, or HTTP on localhost for a local model server.");
   if(Provider=="Claude"&&Endpoint!="https://api.anthropic.com/v1/messages")throw new InvalidDataException("Claude uses the official Anthropic endpoint.");
   if(Provider=="OpenAI"&&Endpoint!="https://api.openai.com/v1/chat/completions")throw new InvalidDataException("OpenAI uses the official OpenAI endpoint.");
  }
  public string CredentialId {get{return Provider+"|"+new Uri(Endpoint).AbsoluteUri;}}
  public string ChatId {get{return CredentialId+"|"+Model;}}
  public AiConfig Copy(){return new JavaScriptSerializer().Deserialize<AiConfig>(new JavaScriptSerializer().Serialize(this));}
 }
 public class ChatMessage { public string role;public string content; }
 public class SecretStore {
  readonly string folder;
  public SecretStore(string folder){this.folder=folder;}
  string PathFor(string id) {using(var hash=SHA256.Create()){return Path.Combine(folder,BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-","")+".protected");}}
  public bool Exists(string id){return File.Exists(PathFor(id));}
  public void Save(string id,string value) {
   Directory.CreateDirectory(folder);byte[] clear=Encoding.UTF8.GetBytes(value);
   try{byte[] bytes=ProtectedData.Protect(clear,Encoding.UTF8.GetBytes("WinCanvas/"+id),DataProtectionScope.CurrentUser);string path=PathFor(id),temp=path+".tmp";File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{Array.Clear(clear,0,clear.Length);}
  }
  public string Read(string id) {
   string path=PathFor(id);if(!File.Exists(path))return "";
   if(new FileInfo(path).Length>2*1024*1024)throw new InvalidDataException("Protected storage is too large.");
   byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(path),Encoding.UTF8.GetBytes("WinCanvas/"+id),DataProtectionScope.CurrentUser);
   try{return Encoding.UTF8.GetString(clear);}finally{Array.Clear(clear,0,clear.Length);}
  }
  public void Delete(string id){string path=PathFor(id);if(File.Exists(path))File.Delete(path);}
  public void DeleteAll(){if(Directory.Exists(folder))foreach(string path in Directory.GetFiles(folder,"*.protected"))File.Delete(path);}
 }
 public interface IAiTransport {Task<string> Send(AiConfig config,string key,IList<ChatMessage> messages,IProgress<string> progress,CancellationToken token);}
 public class AiClient : IAiTransport,IDisposable {
  readonly HttpClient http;
  public AiClient():this(new HttpClientHandler{AllowAutoRedirect=false}){}
  public AiClient(HttpMessageHandler handler){http=new HttpClient(handler);http.Timeout=Timeout.InfiniteTimeSpan;}
  public void Dispose(){http.Dispose();}
  public static HttpRequestMessage BuildRequest(AiConfig config,string key,IList<ChatMessage> history) {
   config.Validate(true);
   if((config.Provider=="Claude"||config.Provider=="OpenAI")&&String.IsNullOrWhiteSpace(key))throw new InvalidOperationException("Save your provider API key in Studio → AI assistant first.");
   if(key!=null&&(key.Length>4096||key.Any(Char.IsControl)))throw new InvalidDataException("Invalid API key.");
   var messages=history.Select(m=>new Dictionary<string,object>{{"role",m.role},{"content",m.content}}).ToList();
   var payload=new Dictionary<string,object>{{"model",config.Model},{"messages",messages},{"stream",true}};
   var request=new HttpRequestMessage(HttpMethod.Post,config.Endpoint);
   if(config.Provider=="Claude") {
    request.Headers.Add("x-api-key",key);request.Headers.Add("anthropic-version","2026-10-21");
    payload["max_tokens"]=config.MaxTokens;payload["system"]=config.Instructions;
   }else {
    messages.Insert(0,new Dictionary<string,object>{{"role","system"},{"content",config.Instructions}});
    if(!String.IsNullOrWhiteSpace(key))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
    if(config.Provider=="Ollama")payload["options"]=new Dictionary<string,object>{{"num_predict",config.MaxTokens}};
    else payload[config.Provider=="OpenAI"?"max_completion_tokens":"max_tokens"]=config.MaxTokens;
   }
   if(config.ImagePath!=null&&File.Exists(config.ImagePath)) {
    // The shared screenshot rides on the latest user message, in each protocol's own format.
    string data=Convert.ToBase64String(File.ReadAllBytes(config.ImagePath));var last=messages[messages.Count-1];string text=(string)last["content"];
    if(config.Provider=="Ollama")last["images"]=new[]{data};
    else if(config.Provider=="Claude")last["content"]=new object[]{new Dictionary<string,object>{{"type","image"},{"source",new Dictionary<string,object>{{"type","base64"},{"media_type","image/png"},{"data",data}}}},new Dictionary<string,object>{{"type","text"},{"text",text}}};
    else last["content"]=new object[]{new Dictionary<string,object>{{"type","text"},{"text",text}},new Dictionary<string,object>{{"type","image_url"},{"image_url",new Dictionary<string,object>{{"url","data:image/png;base64,"+data}}}}};
   }
   request.Content=new StringContent(new JavaScriptSerializer().Serialize(payload),Encoding.UTF8,"application/json");return request;
  }
  internal static Dictionary<string,object> Obj(object value){return value as Dictionary<string,object>;}
  internal static object Get(Dictionary<string,object> value,string key){object result;return value!=null&&value.TryGetValue(key,out result)?result:null;}
  internal static string Str(Dictionary<string,object> value,string key){return Get(value,key) as string;}
  public static string ParseDelta(string provider,string json) {
   var root=new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string,object>;
   if(root==null)throw new InvalidDataException("Unexpected provider response.");
   if(Get(root,"error")!=null || Str(root,"type")=="error")throw new InvalidOperationException("The provider returned a stream error. Check your model, limits, and provider status.");
   if(provider=="Claude") {var delta=Obj(Get(root,"delta"));return Str(delta,"type")=="text_delta"?Str(delta,"text")??"":"";}
   if(provider=="Ollama")return Str(Obj(Get(root,"message")),"content")??"";
   var choices=Get(root,"choices") as object[];
   return choices!=null&&choices.Length>0?Str(Obj(Get(Obj(choices[0]),"delta")),"content")??"":"";
  }
  public async Task<string> Send(AiConfig config,string key,IList<ChatMessage> messages,IProgress<string> progress,CancellationToken token) {
   using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)) {
    deadline.CancelAfter(TimeSpan.FromMinutes(3));
    try {
     using(var request=BuildRequest(config,key,messages))
     using(var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token).ConfigureAwait(false)) {
      if(!response.IsSuccessStatusCode){int code=(int)response.StatusCode;throw new InvalidOperationException(code==401||code==403?"Provider rejected authentication or access. Check your API key and model access.":code==429?"Provider rate limit or quota reached. Check billing/limits and try later.":"Provider returned HTTP "+code+". Check the endpoint, model ID, and service status.");}
      using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
      using(deadline.Token.Register(()=>stream.Dispose()))
      using(var reader=new StreamReader(stream,Encoding.UTF8)) {
       var answer=new StringBuilder();string line;int received=0;bool complete=false;
       while((line=await reader.ReadLineAsync().ConfigureAwait(false))!=null) {
        deadline.Token.ThrowIfCancellationRequested();received+=line.Length;if(received>2*1024*1024)throw new InvalidDataException("Provider response exceeded the size limit.");
        if(String.IsNullOrWhiteSpace(line)||line.StartsWith(":"))continue;
        string json=line;
        if(config.Provider!="Ollama") {if(!line.StartsWith("data:"))continue;json=line.Substring(5).TrimStart();if(json=="[DONE]"){complete=true;break;}}
        var parsed=Obj(new JavaScriptSerializer().DeserializeObject(json));
        string chunk=ParseDelta(config.Provider,json);
        if(answer.Length+chunk.Length>16000)throw new InvalidDataException("Reply exceeded 16,000 characters. Lower the response limit or ask a shorter question.");
        if(chunk.Length>0){answer.Append(chunk);progress.Report(chunk);}
        if(config.Provider=="Claude"&&Str(parsed,"type")=="message_stop"){complete=true;break;}
        if(config.Provider=="Ollama"&&Object.Equals(Get(parsed,"done"),true)){complete=true;break;}
       }
       deadline.Token.ThrowIfCancellationRequested();
       if(!complete)throw new IOException("The connection ended before the reply completed. Partial text was not added to history.");
       if(answer.Length==0)throw new InvalidDataException("The model returned no text. Try a text-capable model or raise the response limit.");
       return answer.ToString();
      }
     }
    }catch(Exception){if(token.IsCancellationRequested)throw new OperationCanceledException(token);if(deadline.IsCancellationRequested)throw new TimeoutException("The provider took over three minutes. Try a smaller model or a shorter request.");throw;}
   }
  }
 }
 public class ChatSession {
  readonly IAiTransport transport;readonly SecretStore credentials,historyStore;
  public AiConfig Config;public List<ChatMessage> Messages=new List<ChatMessage>();
  public string StorageWarning="";
  // Set just before Send to share a screenshot with that one message.
  public string Attach;
  public bool Busy {get;private set;} CancellationTokenSource pending;
  public ChatSession(AiConfig config,IAiTransport transport,SecretStore credentials,SecretStore historyStore){Config=config;this.transport=transport;this.credentials=credentials;this.historyStore=historyStore;}
  public static void ValidateHistory(List<ChatMessage> messages) {
   if(messages==null||messages.Count>12||messages.Count%2!=0)throw new InvalidDataException("Invalid chat history.");
   int total=0;for(int i=0;i<messages.Count;i++){var m=messages[i];if(m==null||m.role!=(i%2==0?"user":"assistant")||m.content==null||m.content.Length>16000)throw new InvalidDataException("Invalid chat message.");total+=m.content.Length;}if(total>48000)throw new InvalidDataException("Chat history is too large.");
  }
  public void Load() {Messages.Clear();if(Config.RememberChat){string json=historyStore.Read(Config.ChatId);if(json.Length>0){var items=new JavaScriptSerializer().Deserialize<List<ChatMessage>>(json);ValidateHistory(items);Messages=items;}}}
  public void Cancel(){if(pending!=null)pending.Cancel();}
  public void Clear(){Cancel();Messages.Clear();historyStore.Delete(Config.ChatId);}
  public async Task<string> Send(string text,IProgress<string> progress) {
   if(Busy)throw new InvalidOperationException("Wait for the current reply or press Stop.");
   if(String.IsNullOrWhiteSpace(text)||text.Length>8000)throw new InvalidDataException("Write a message of 1–8,000 characters.");
   Config.Validate(true);var config=Config.Copy();config.ImagePath=Attach;Attach=null;if(config.AllowActions)config.Instructions+=PcActions.Protocol;var context=Messages.Select(m=>new ChatMessage{role=m.role,content=m.content}).ToList();
   while(context.Count>10||context.Sum(m=>m.content.Length)+text.Length>32000){context.RemoveRange(0,2);}
   context.Add(new ChatMessage{role="user",content=text});
   Busy=true;StorageWarning="";pending=new CancellationTokenSource();
   try {
    string reply=await transport.Send(config,credentials.Read(config.CredentialId),context,progress,pending.Token);
    pending.Token.ThrowIfCancellationRequested();context.Add(new ChatMessage{role="assistant",content=reply});
    while(context.Count>12||context.Sum(m=>m.content.Length)>48000)context.RemoveRange(0,2);
    ValidateHistory(context);Messages=context;
    if(config.RememberChat){try{historyStore.Save(config.ChatId,new JavaScriptSerializer().Serialize(Messages));}catch(Exception){StorageWarning="Reply received, but local chat could not be saved. It remains in memory.";}}
    return reply;
   }finally{pending.Dispose();pending=null;Busy=false;}
  }
 }
}
