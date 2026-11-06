using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace WinCanvas {
 public class IslandChat : StackPanel {
  readonly ChatSession session;readonly TextBox input=new TextBox(),transcript=new TextBox();readonly TextBlock status=new TextBlock();readonly Button send,stop,signIn;
  readonly StackPanel actions=new StackPanel();readonly Palette p;readonly string background;
  public IPcHost Host;
  // Switches who answers (Claude or ChatGPT) right from the island; the host saves the choice.
  public Action<string> ChangeEngine;readonly WrapPanel engines=new WrapPanel{Margin=new Thickness(0,0,0,8)};
  static readonly string[][] Engines={new[]{"Auto","Auto"},new[]{"Claude Code","Claude"},new[]{"Codex","ChatGPT"}};
  void ShowEngines(){engines.Children.Clear();foreach(var e in Engines){string id=e[0];bool on=session.Config.Provider==id;var b=Pill(e[1],on?"#FFFFFF":p.Field,on?"#000000":p.Soft,()=>{if(session.Busy){status.Text="Stop the current reply first.";return;}if(ChangeEngine!=null)ChangeEngine(id);else session.Config.Provider=id;ShowEngines();transcript.ToolTip=Describe();status.Text=(id=="Codex"?"ChatGPT":id=="Claude Code"?"Claude":"Auto")+" answers your next message.";});b.Height=28;b.FontSize=12;b.Margin=new Thickness(0,0,6,0);engines.Children.Add(b);}}
  bool shareScreen;Button screen;
  void Share(bool on){shareScreen=on;bool always=session.Config.AlwaysScreen;screen.Background=Brush(on||always?"#0A84FF":p.Field);var label=(TextBlock)screen.Content;label.Foreground=Brush(on||always?"#FFFFFF":p.Soft);((Run)label.Inlines.LastInline).Text=always?"  Screen: always":on?"  Screen: on":"  Screen";screen.ToolTip=always?"Your screen is shared with every message (change this in WinDotra → AI).":on?"Your screen will be shared with this message. Click to cancel.":"Share a screenshot of your screen with the next message. Asking about your screen (“what's on my screen?”) shares it automatically.";}
  // Messages that are clearly about what's on screen share it automatically (English and German).
  static readonly System.Text.RegularExpressions.Regex ScreenTalk=new System.Text.RegularExpressions.Regex(@"\b(screen|screenshot|display|monitor|bildschirm|look at|see (this|that|my|what)|what('s| is) (this|that|on|here)|this (window|page|error|app|image|picture|code)|siehst du|schau (mal|dir)|was ist (das|hier)|auf meinem)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
  bool disposed;string before="";
  static SolidColorBrush Brush(string color){return Ui.Brush(color);}
  Button Link(string label,Action action){var b=new Button{Content=label,Background=Brushes.Transparent,Foreground=Brush(p.Soft),BorderThickness=new Thickness(0),Padding=new Thickness(8,4,8,4),Margin=new Thickness(0,0,2,0),FontSize=12,Cursor=Cursors.Hand};b.Click+=(s,e)=>{try{action();}catch(Exception ex){status.Text=ex.Message;}};return b;}
  Button Pill(string label,string back,string fore,Action action){var b=new Button{Content=label,Background=Brush(back),Foreground=Brush(fore),BorderThickness=new Thickness(0),Padding=new Thickness(14,0,14,0),Height=34,Margin=new Thickness(8,0,0,0),FontWeight=FontWeights.SemiBold,Cursor=Cursors.Hand,VerticalAlignment=VerticalAlignment.Bottom};b.Template=Round(17);b.Click+=(s,e)=>{try{action();}catch(Exception ex){status.Text=ex.Message;}};return b;}
  static ControlTemplate Round(double radius){var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(radius));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);border.AppendChild(content);template.VisualTree=border;var off=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};off.Setters.Add(new Setter(UIElement.OpacityProperty,0.4));template.Triggers.Add(off);return template;}
  public IslandChat(ChatSession session,Action configure,string accent):this(session,configure,accent,new Palette("#FFFFFF",accent),"#FFFFFF"){}
  public IslandChat(ChatSession session,Action configure,string accent,Palette palette,string background) {
   this.session=session;p=palette;this.background=background;
   Children.Add(engines);ShowEngines();
   transcript.IsReadOnly=true;transcript.TextWrapping=TextWrapping.Wrap;transcript.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;transcript.Height=130;transcript.Background=Brush(p.Field);transcript.Foreground=Brush(p.Ink);transcript.BorderThickness=new Thickness(0);transcript.Padding=new Thickness(12,10,12,10);transcript.FontSize=13;transcript.ToolTip=Describe();Children.Add(transcript);Render();
   actions.Margin=new Thickness(0,6,0,0);Children.Add(actions);
   var row=new Grid{Margin=new Thickness(0,8,0,0)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   // Screen sharing is off by default and switches itself off again after each message.
   screen=Pill("",p.Field,p.Soft,()=>Share(!shareScreen));var screenLabel=new TextBlock{FontSize=12};screenLabel.Inlines.Add(new Run("\uE7F4"){FontFamily=new FontFamily(Ui.Icons),FontSize=13});screenLabel.Inlines.Add(new Run("  Screen"));screen.Content=screenLabel;screen.Padding=new Thickness(12,0,12,0);screen.Margin=new Thickness(0,0,8,0);row.Children.Add(screen);Share(false);
   input.AcceptsReturn=true;input.TextWrapping=TextWrapping.Wrap;input.MinHeight=34;input.MaxHeight=90;input.MaxLength=8000;input.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;input.Background=Brush(p.Field);input.Foreground=Brush(p.Ink);input.CaretBrush=Brush(p.Ink);input.BorderThickness=new Thickness(0);input.Padding=new Thickness(12,7,12,7);input.FontSize=13;input.ToolTip="Ask anything, or ask it to do something on your PC. Enter sends; Shift+Enter for a new line.";Grid.SetColumn(input,1);row.Children.Add(input);
   var buttons=new Grid();Grid.SetColumn(buttons,2);send=Pill("Send",p.Ink,background,()=>Send());stop=Pill("Stop",p.Chip,p.Ink,()=>session.Cancel());stop.IsEnabled=false;stop.Visibility=Visibility.Collapsed;buttons.Children.Add(send);buttons.Children.Add(stop);row.Children.Add(buttons);Children.Add(row);
   var links=new WrapPanel{Margin=new Thickness(-8,6,0,0)};
   links.Children.Add(Link("New chat",()=>{if(session.Busy){status.Text="Stop the current reply first.";return;}session.Clear();actions.Children.Clear();Render();status.Text="Conversation cleared.";}));
   links.Children.Add(Link("Copy",()=>{var last=session.Messages.LastOrDefault(m=>m.role=="assistant");if(last!=null){Clipboard.SetText(PcActions.Strip(last.content));status.Text="Reply copied.";}}));
   links.Children.Add(Link("Browser ↗",()=>{if(String.IsNullOrWhiteSpace(input.Text)){status.Text="Type a question first.";return;}status.Text=BrowserAi.Open(session.Config.BrowserSite,input.Text.Trim());input.Clear();}));
   signIn=Link("Sign in",()=>{if(session.Config.Provider=="Codex"){Codex.SignIn();status.Text="Finish signing in with your ChatGPT account in the window that opens. Then ask again here.";}else{ClaudeCode.SignIn();status.Text="In the Claude window, type /login and follow the steps. Then ask again here.";}});signIn.Foreground=Brush("#0A84FF");signIn.Visibility=Visibility.Collapsed;links.Children.Add(signIn);
   links.Children.Add(Link("Setup",configure));Children.Add(links);
   status.Text="";status.TextWrapping=TextWrapping.Wrap;status.FontSize=11;status.Foreground=Brush(p.Soft);status.Margin=new Thickness(0,2,0,0);Children.Add(status);
   // Enter sends, Shift+Enter (or Ctrl+Enter) adds nothing special: Shift+Enter makes a new line.
   input.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Shift)==0){e.Handled=true;Send();}};
  }
  string Describe(){var c=session.Config;return c.Provider=="Auto"?"Auto: local AI if found, then your Claude, otherwise "+c.BrowserSite+" in your browser":c.Provider=="Browser"?"Browser: opens "+c.BrowserSite:c.Provider=="Claude Code"?"Claude: your Claude subscription":c.Provider=="Codex"?"ChatGPT: your ChatGPT account":c.Provider+(String.IsNullOrEmpty(c.Model)?"":" · "+c.Model);}
  void Render(){var b=new StringBuilder();foreach(var m in session.Messages){string text=m.role=="user"?m.content:PcActions.Strip(m.content);b.AppendLine(m.role=="user"?"You":"Assistant").AppendLine(text.Length>0?text:"(suggested an action)").AppendLine();}transcript.Text=b.Length==0?"Ask anything — or ask me to do something, like “open Spotify” or “start a 25 minute focus”. You confirm every action.":b.ToString().TrimEnd();transcript.ScrollToEnd();}
  // One card per suggested action; nothing runs until Run is clicked.
  void ShowActions(string reply) {
   actions.Children.Clear();if(Host==null)return;
   foreach(var action in PcActions.Parse(reply)) {
    var item=action;var card=new Grid{Margin=new Thickness(0,0,0,6)};card.ColumnDefinitions.Add(new ColumnDefinition());card.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});card.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var back=new Border{Background=Brush(p.Field),CornerRadius=new CornerRadius(14),Padding=new Thickness(12,8,8,8)};var inner=new Grid();inner.ColumnDefinitions.Add(new ColumnDefinition());inner.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});inner.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var text=new TextBlock{Text=item.Describe(),FontSize=13,Foreground=Brush(p.Ink),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};inner.Children.Add(text);
    Button run=null,skip=null;
    run=Pill("Run","#0A84FF","#FFFFFF",()=>{try{text.Text=PcActions.Run(item,Host);text.Foreground=Brush(p.Soft);}catch(Exception ex){text.Text=ex.Message;}run.Visibility=skip.Visibility=Visibility.Collapsed;});run.Height=28;Grid.SetColumn(run,1);inner.Children.Add(run);
    skip=Pill("Skip",p.Chip,p.Ink,()=>{text.Text="Skipped: "+item.Describe();text.Foreground=Brush(p.Soft);run.Visibility=skip.Visibility=Visibility.Collapsed;});skip.Height=28;skip.Margin=new Thickness(6,0,0,0);Grid.SetColumn(skip,2);inner.Children.Add(skip);
    back.Child=inner;actions.Children.Add(back);
   }
  }
  string Browser(string question,string shot){string result=BrowserAi.Open(session.Config.BrowserSite,question);if(shot!=null){Clipboard.SetImage(new System.Windows.Media.Imaging.BitmapImage(new Uri(shot)));result+=" Your screenshot is on the clipboard: paste it into the chat.";}return result;}
  // Screenshots are deleted a few seconds after use, once the AI or the clipboard has them.
  static void Forget(string shot){if(shot!=null)System.Threading.Tasks.Task.Delay(30000).ContinueWith(t=>ScreenShot.Forget(shot));}
  public void FocusInput(){input.Focus();input.CaretIndex=input.Text.Length;}
  public void Close(){disposed=true;session.Cancel();}
  void Busy(bool busy){send.IsEnabled=!busy;stop.IsEnabled=busy;send.Visibility=busy?Visibility.Collapsed:Visibility.Visible;stop.Visibility=busy?Visibility.Visible:Visibility.Collapsed;input.IsReadOnly=busy;}
  async void Send() {
   if(disposed||session.Busy||String.IsNullOrWhiteSpace(input.Text))return;
   string question=input.Text;actions.Children.Clear();signIn.Visibility=Visibility.Collapsed;string shot=null;
   bool automatic=!shareScreen&&!session.Config.AlwaysScreen&&session.Config.AutoScreen&&ScreenTalk.IsMatch(question);
   if(shareScreen||session.Config.AlwaysScreen||automatic){try{shot=ScreenShot.Capture(Window.GetWindow(this));}catch(Exception e){status.Text="Couldn't capture the screen: "+e.Message;return;}Share(false);status.Text=automatic?"Sharing your screen because you asked about it…":"Sharing your screen…";}
   string provider=session.Config.Provider;
   if(provider=="Claude app"||provider=="ChatGPT app"){Busy(true);try{status.Text=await AiApps.Ask(provider,question.Trim(),shot);input.Clear();}catch(Exception e){status.Text=e.Message;}finally{Busy(false);Forget(shot);}return;}
   if(provider=="Browser"){status.Text=Browser(question.Trim(),shot);input.Clear();Forget(shot);return;}
   session.Attach=shot;NoLocalAiException fallback=null;
   before=session.Messages.Count==0?"":String.Join("\n\n",session.Messages.Select(m=>(m.role=="user"?"You\n"+m.content:"Assistant\n"+PcActions.Strip(m.content))))+"\n\n";
   transcript.Text=before+"You\n"+question+"\n\nAssistant\n";Busy(true);status.Text="Thinking…";
   bool receiving=true;
   var progress=new Progress<string>(chunk=>{if(disposed||!receiving)return;transcript.AppendText(chunk);transcript.ScrollToEnd();status.Text="Receiving reply…";});
   try {string reply=await session.Send(question,progress);receiving=false;if(!disposed){input.Clear();Render();ShowActions(reply);status.Text=session.StorageWarning.Length>0?session.StorageWarning:actions.Children.Count>0?"Check the action and click Run if it's right.":"";}}
   catch(OperationCanceledException){if(!disposed){Render();status.Text="Stopped. Your draft is kept.";}}
   catch(NoLocalAiException e){fallback=e;}
   catch(ClaudeSignInException e){if(!disposed){Render();status.Text=e.Message;signIn.Visibility=Visibility.Visible;}}
   catch(Exception e){if(!disposed){Render();status.Text=e.Message;if(e.Message.Contains("sign in"))signIn.Visibility=Visibility.Visible;}}
   finally{receiving=false;session.Attach=null;Forget(shot);if(!disposed)Busy(false);}
   // Nothing answered inside the island: hand the question to an installed app, or the browser.
   if(fallback!=null&&!disposed){Render();string app=AiApps.FallbackFor(session.Config);try{status.Text=(app!=null?await AiApps.Ask(app,question.Trim(),shot):Browser(question.Trim(),shot))+" ("+fallback.Message+")";}catch(Exception ex){status.Text=ex.Message;}input.Clear();if(ClaudeCode.Find()!=null&&app==null)signIn.Visibility=Visibility.Visible;}
  }
 }
}
