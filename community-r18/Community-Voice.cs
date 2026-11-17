using System;
using System.Diagnostics;
using System.Windows.Automation;
using System.Threading;

public sealed class VoiceProbe {public bool Closing,NeedsToggle,Open;}

public static class WindowsVoice {
 public static bool Owns(uint pid){try{using(var p=Process.GetProcessById((int)pid))return p.ProcessName.Equals("TextInputHost",StringComparison.OrdinalIgnoreCase)||p.ProcessName.Equals("InputApp",StringComparison.OrdinalIgnoreCase);}catch{return false;}}
 static AutomationElement FindPanel(){
  if(Thread.CurrentThread.GetApartmentState()!=ApartmentState.MTA)throw new InvalidOperationException("Windows voice inspection must run on its MTA worker.");
  try{
   var windows=AutomationElement.RootElement.FindAll(TreeScope.Children,Condition.TrueCondition);
   foreach(AutomationElement window in windows){
    if(!Owns((uint)window.Current.ProcessId)||window.Current.IsOffscreen)continue;
    string title=window.Current.Name??"";
    if(title.IndexOf("voice typing",StringComparison.OrdinalIgnoreCase)>=0||title.IndexOf("dictation",StringComparison.OrdinalIgnoreCase)>=0)return window;
    var panel=window.FindFirst(TreeScope.Descendants,new OrCondition(new PropertyCondition(AutomationElement.NameProperty,"Voice typing"),new PropertyCondition(AutomationElement.NameProperty,"Dictation"),new PropertyCondition(AutomationElement.AutomationIdProperty,"VoiceTypingMainWindow"),new PropertyCondition(AutomationElement.AutomationIdProperty,"DictationMainWindow")));
    if(panel!=null&&!panel.Current.IsOffscreen)return window;
   }
  }catch{}
  return null;
 }
 public static bool IsOpen(){return FindPanel()!=null;}
 // A true result authorizes the standard toggle only after observing the actual panel.
 public static bool DismissOrNeedsToggle(out bool needsToggle){
  needsToggle=false;var panel=FindPanel();if(panel==null)return false;
  try{
   var close=panel.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),new OrCondition(new PropertyCondition(AutomationElement.NameProperty,"Close"),new PropertyCondition(AutomationElement.NameProperty,"Close voice typing"))));
   object pattern;if(close!=null&&close.TryGetCurrentPattern(InvokePattern.Pattern,out pattern)){((InvokePattern)pattern).Invoke();return true;}
  }catch{}
  needsToggle=true;return true;
 }
}

