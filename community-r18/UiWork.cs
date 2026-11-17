using System;
using System.Threading;

// At most one provider call per instance. A timeout releases the UI callback,
// while the occupied worker remains bounded until that provider really returns.
public sealed class UiWork<T>:IDisposable {
 readonly object gate=new object();bool running,ready,expired,disposed;
 T value;Exception error;DateTime deadline;Action<T,Exception> callback;
 public bool Busy {get{lock(gate)return running||callback!=null;}}
 public bool Start(Func<T> work,TimeSpan timeout,Action<T,Exception> completion){
  lock(gate){if(disposed||running||callback!=null)return false;running=true;ready=false;expired=false;deadline=DateTime.UtcNow+timeout;callback=completion;}
  var thread=new Thread(()=>{
   T result=default(T);Exception failure=null;
   try{result=work();}catch(Exception e){failure=e;}
   lock(gate){running=false;if(!expired&&!disposed){value=result;error=failure;ready=true;}}
  });
  thread.IsBackground=true;thread.Name="Morbid Kitty optional service";thread.SetApartmentState(ApartmentState.MTA);thread.Start();return true;
 }
 public void Pump(DateTime now){
  Action<T,Exception> done=null;T result=default(T);Exception failure=null;
  lock(gate){
   if(callback==null||disposed)return;
   if(ready){done=callback;result=value;failure=error;callback=null;ready=false;value=default(T);error=null;}
   else if(now>=deadline){expired=true;done=callback;callback=null;failure=new TimeoutException("Windows did not finish this request in time. The other controls remain available.");}
  }
  if(done!=null)done(result,failure);
 }
 public void Dispose(){lock(gate){disposed=true;expired=true;callback=null;value=default(T);error=null;}}
}
