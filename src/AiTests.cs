using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WinCanvas {
 class CaptureProgress : IProgress<string>{public string Text="";public void Report(string value){Text+=value;}}
 class MockHttp : HttpMessageHandler {
  public string Body;public HttpStatusCode Code=HttpStatusCode.OK;public HttpRequestMessage Request;public string Payload;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Request=request;Payload=await request.Content.ReadAsStringAsync();return new HttpResponseMessage(Code){Content=new StringContent(Body??"",Encoding.UTF8,"text/event-stream")};}
 }
 class MockAi : IAiTransport {
  public bool Fail,Wait;public List<ChatMessage> Context;
  public async Task<string> Send(AiConfig config,string key,IList<ChatMessage> messages,IProgress<string> progress,CancellationToken token){Context=messages.ToList();if(Wait)await Task.Delay(Timeout.Infinite,token);if(Fail)throw new IOException("Simulated connection failure");progress.Report("Mock reply");return "Mock reply";}
 }
 public static class AiTests {
  static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root){yield return root;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants(System.Windows.Media.VisualTreeHelper.GetChild(root,i)))yield return child;}
  static void Pump(){var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);}
  public static void SmokeUi(string directory,System.Windows.ResourceDictionary resources){
   var previous=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
   var backend=new MockAi();var config=new AiConfig{Provider="Ollama",Endpoint="http://localhost:11434/api/chat",Model="simulated-ui-test"};
   var session=new ChatSession(config,backend,new SecretStore(Path.Combine(directory,"ui-keys")),new SecretStore(Path.Combine(directory,"ui-history")));
   var panel=new IslandChat(session,()=>{},"#B5A5FF");var window=new System.Windows.Window{Title="WinDotra UI test (simulated provider)",Content=panel,Width=520,SizeToContent=System.Windows.SizeToContent.Height,Resources=resources};
   try{
    window.Show();window.UpdateLayout();
    var boxes=Descendants(panel).OfType<System.Windows.Controls.TextBox>().ToList();var input=boxes.First(b=>!b.IsReadOnly);var transcript=boxes.First(b=>b.IsReadOnly);
    var buttons=Descendants(panel).OfType<System.Windows.Controls.Button>().ToList();var send=buttons.First(b=>b.Content.ToString().StartsWith("Send"));var stop=buttons.First(b=>b.Content.ToString()=="Stop");var clear=buttons.First(b=>b.Content.ToString()=="New chat");
    input.Text="UI test question";send.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Pump();
    Check(session.Messages.Count==2&&input.Text==""&&transcript.Text.Split(new[]{"Mock reply"},StringSplitOptions.None).Length==2,"Chat controls send and render once");
    clear.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Check(session.Messages.Count==0,"New chat control");
    backend.Wait=true;input.Text="Keep this draft";send.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Check(stop.IsEnabled&&!send.IsEnabled,"Busy controls");stop.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Pump();
    Check(!session.Busy&&send.IsEnabled&&!stop.IsEnabled&&input.Text=="Keep this draft"&&session.Messages.Count==0,"Stop control preserves draft");
   }finally{panel.Close();window.Close();SynchronizationContext.SetSynchronizationContext(previous);}
  }
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  static void Reject(Action action){bool failed=false;try{action();}catch{failed=true;}Check(failed,"Expected validation rejection");}
  public static void Run(string directory){RunAsync(directory).GetAwaiter().GetResult();}
  static async Task RunAsync(string directory) {
   var config=new AiConfig{Provider="Claude",Endpoint="https://api.anthropic.com/v1/messages",Model="test-model"};config.Validate(true);
   foreach(string url in new[]{"http://evil.example/v1","file:///c:/secret","https://user:pass@example.com/v1","https://example.com/v1?key=secret","https://example.com/v1#fragment"}) {var c=new AiConfig{Provider="Compatible",Endpoint=url};Reject(()=>c.Validate(false));}
   var local=new AiConfig{Provider="Ollama",Endpoint="http://localhost:11434/api/chat",Model="local-test"};local.Validate(true);
   var messages=new List<ChatMessage>{new ChatMessage{role="user",content="Hello"}};
   using(var request=AiClient.BuildRequest(config,"unit-test-key",messages)) {
    var payload=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(await request.Content.ReadAsStringAsync());
    Check(request.RequestUri.Host=="api.anthropic.com"&&request.Headers.Contains("x-api-key")&&payload.ContainsKey("system")&&payload.ContainsKey("max_tokens"),"Claude request schema");
   }
   var openai=new AiConfig{Provider="OpenAI",Endpoint="https://api.openai.com/v1/chat/completions",Model="test-model"};
   using(var request=AiClient.BuildRequest(openai,"unit-test-key",messages))Check((await request.Content.ReadAsStringAsync()).Contains("max_completion_tokens")&&request.Headers.Authorization.Scheme=="Bearer","OpenAI schema");
   using(var request=AiClient.BuildRequest(local,"",messages))Check((await request.Content.ReadAsStringAsync()).Contains("num_predict")&&request.Headers.Authorization==null,"Ollama no key required");
   Reject(()=>AiClient.BuildRequest(config,"",messages));Reject(()=>AiClient.BuildRequest(config,"bad\nkey",messages));
   var wire=new MockHttp{Body="event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello 🌍\"}}\n\ndata: {\"type\":\"message_stop\"}\n"};var progress=new CaptureProgress();
   using(var client=new AiClient(wire))Check(await client.Send(config,"unit-test-key",messages,progress,CancellationToken.None)=="Hello 🌍"&&progress.Text=="Hello 🌍","Claude SSE Unicode");
   wire=new MockHttp{Body="data: {\"choices\":[{\"delta\":{\"content\":\"Hi\"}}]}\n\ndata: [DONE]\n"};
   using(var client=new AiClient(wire))Check(await client.Send(openai,"unit-test-key",messages,new CaptureProgress(),CancellationToken.None)=="Hi","OpenAI SSE");
   wire=new MockHttp{Body="{\"message\":{\"content\":\"Local\"},\"done\":false}\n{\"message\":{\"content\":\" reply\"},\"done\":true}\n"};
   using(var client=new AiClient(wire))Check(await client.Send(local,"",messages,new CaptureProgress(),CancellationToken.None)=="Local reply","Ollama NDJSON");
   wire=new MockHttp{Body="data: {\"choices\":[{\"delta\":{\"content\":\"Partial\"}}]}\n"};
   using(var client=new AiClient(wire)){bool failed=false;try{await client.Send(openai,"unit-test-key",messages,new CaptureProgress(),CancellationToken.None);}catch(IOException){failed=true;}Check(failed,"Interrupted stream rejected");}
   wire=new MockHttp{Code=HttpStatusCode.Unauthorized,Body="secret-token-never-display"};
   using(var client=new AiClient(wire)){bool failed=false;try{await client.Send(config,"unit-test-key",messages,new CaptureProgress(),CancellationToken.None);}catch(InvalidOperationException e){failed=!e.Message.Contains("secret-token");}Check(failed,"Errors do not echo provider body");}
   Reject(()=>AiClient.ParseDelta("Claude","{\"type\":\"error\",\"error\":{}}"));
   var keys=new SecretStore(Path.Combine(directory,"test-keys"));var history=new SecretStore(Path.Combine(directory,"test-history"));keys.Save(config.CredentialId,"test-secret-123");Check(keys.Read(config.CredentialId)=="test-secret-123"&&keys.Read(openai.CredentialId)=="","Encrypted credentials and endpoint binding");
   Check(!Directory.GetFiles(Path.Combine(directory,"test-keys")).Any(p=>Encoding.UTF8.GetString(File.ReadAllBytes(p)).Contains("test-secret-123")),"No plaintext key on disk");
   var fake=new MockAi();config.RememberChat=true;var session=new ChatSession(config,fake,keys,history);await session.Send("Question",new CaptureProgress());Check(session.Messages.Count==2&&history.Exists(config.ChatId),"Chat saved after completion");
   session.Load();Check(session.Messages[1].content=="Mock reply","Encrypted history reload");fake.Fail=true;try{await session.Send("Failing question",new CaptureProgress());}catch(IOException){}Check(session.Messages.Count==2&&!session.Busy,"Failed send preserves completed history");
   fake.Fail=false;fake.Wait=true;var pending=session.Send("Cancel me",new CaptureProgress());Check(session.Busy,"In-flight state");session.Cancel();try{await pending;}catch(OperationCanceledException){}Check(!session.Busy&&session.Messages.Count==2,"Cancellation keeps history clean");
   session.Clear();Check(session.Messages.Count==0&&!history.Exists(config.ChatId),"New chat removes stored history");
   fake.Wait=false;config.RememberChat=false;await session.Send("Ephemeral",new CaptureProgress());Check(!history.Exists(config.ChatId),"Memory-only default");
   config.RememberChat=true;for(int i=0;i<10;i++)await session.Send("Question "+i,new CaptureProgress());Check(session.Messages.Count==12,"Bounded conversation");
   keys.Delete(config.CredentialId);Check(!keys.Exists(config.CredentialId),"Key removal");history.DeleteAll();Check(!history.Exists(config.ChatId),"History removal");
   var profile=new DesktopProfile();profile.Tasks.Add(new ChecklistItem{Text="Local task"});profile.Validate();string json=new JavaScriptSerializer().Serialize(profile);Check(!json.Contains("test-secret")&&!json.Contains("Endpoint")&&!json.Contains("Instructions"),"Profiles exclude AI credentials and conversation");
  }
 }
}
