(function(){
  const state={model:"AionUI",status:null,tools:[],receipts:[],scopes:[{id:"local_vault",label:"Local Vault",enabled:true},{id:"pdm",label:"PDM",enabled:false},{id:"epicor",label:"Epicor",enabled:false},{id:"all",label:"Both/All",enabled:true}],scope:"local_vault",messages:[],stream:null};
  const root=document.getElementById("root");
  function esc(v){return String(v==null?"":v).replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));}
  function field(o,a,b,d){return o&&o[a]!=null?o[a]:(o&&o[b]!=null?o[b]:d);}
  function render(){
    const activeScope=state.scope||"local_vault";
    root.innerHTML=`<main class="shell">
      <header class="header">
        <div class="title"><span class="mark"></span><span>BlueBrick Assistant</span></div>
        <div class="controls">
          <select id="assistant-model" name="assistant-model" class="select" aria-label="Model selector"><option>${esc(state.model||"AionUI")}</option></select>
          <div class="scopes">${state.scopes.map(s=>`<button class="scope ${s.id===activeScope?"selected":""}" data-scope="${esc(s.id)}" ${s.enabled===false?"disabled":""} title="${esc(s.unavailableReason||s.description||s.label)}"><span>${esc(s.label)}</span></button>`).join("")}</div>
          <div class="chips">${chips().map(c=>`<span class="chip">${esc(c)}</span>`).join("")}</div>
        </div>
      </header>
      <section class="thread" id="thread">${state.messages.map(renderMessage).join("")}</section>
      <footer class="footer"><div class="input">Chat</div><div class="iconbar"><button class="icon" title="New session">+</button><button class="icon" title="Capture screenshot">□</button><button class="icon" title="Search">⌕</button></div></footer>
    </main><div class="flyout" id="flyout"><button class="close" title="Close">×</button><img class="zoom" alt="Screenshot preview" /></div>`;
    root.querySelectorAll(".scope").forEach(btn=>btn.addEventListener("click",()=>{state.scope=btn.getAttribute("data-scope");render();}));
    root.querySelectorAll("[data-shot]").forEach(img=>img.addEventListener("click",()=>openFlyout(img.src)));
    const thread=document.getElementById("thread"); if(thread) thread.scrollTop=thread.scrollHeight;
  }
  function chips(){
    const caps=[];
    const m=state.status&&state.status.activeModelDescriptor||{};
    if(m.supportsVision||m.SupportsVision)caps.push("vision");
    if(m.supportsToolCalling||m.SupportsToolCalling)caps.push("tools");
    if(m.supportsStructuredOutput||m.SupportsStructuredOutput)caps.push("structured");
    if(!caps.length)caps.push("text");
    return caps;
  }
  function renderMessage(m){
    if(m.kind==="screenshot"){
      const path=m.path||"", thumb=m.thumbnailPath?("file:///"+m.thumbnailPath.replace(/\\/g,"/")):"";
      return `<article class="msg"><div class="role">Screenshot</div><div class="shot">
        ${thumb?`<img data-shot="1" src="${esc(thumb)}" alt="Captured screenshot thumbnail" />`:""}
        <div class="text">${esc(m.message||"Screenshot captured.")}</div>
        <div class="meta"><span>${esc(m.capturedUtc||"")}</span><span>${esc(m.width)} × ${esc(m.height)}</span><span>${esc(m.fileName||path)}</span><span>${esc(m.localOnlyCloudState||"local only")}</span></div>
      </div></article>`;
    }
    return `<article class="msg ${m.role==="user"?"user":""}"><div class="role">${esc(m.role||"assistant")}</div><div class="text">${esc(m.text||"")}</div></article>`;
  }
  function openFlyout(src){const f=document.getElementById("flyout"); if(!f)return; f.querySelector("img").src=src; f.classList.add("open"); f.querySelector(".close").onclick=()=>f.classList.remove("open");}
  window.bbReset=function(){state.messages=[];state.stream=null;render();};
  window.bbAppend=function(raw){const m=typeof raw==="string"?JSON.parse(raw):raw;state.messages.push({role:field(m,"role","Role","assistant"),text:field(m,"content","Content",field(m,"text","Text",""))});render();};
  window.bbTypingStart=function(){state.stream={role:"assistant",text:""};state.messages.push(state.stream);render();};
  window.bbAppendChunk=function(text){if(!state.stream)window.bbTypingStart();state.stream.text+=(text||"");render();};
  window.bbTypingStop=function(){state.stream=null;render();};
  window.bbSetModel=function(name){state.model=name||state.model;render();};
  window.bbSetStatus=function(raw){state.status=typeof raw==="string"?JSON.parse(raw):raw;const scopes=field(state.status,"scopes","Scopes",null);if(Array.isArray(scopes))state.scopes=scopes.map(s=>({id:field(s,"id","Id",""),label:field(s,"label","Label",""),enabled:field(s,"enabled","Enabled",true),description:field(s,"description","Description",""),unavailableReason:field(s,"unavailableReason","UnavailableReason","")}));render();};
  window.bbSetTools=function(raw){state.tools=Array.isArray(raw)?raw:[];};
  window.bbSetToolReceipts=function(raw){state.receipts=Array.isArray(raw)?raw:[];};
  window.bbSetProductCatalogs=function(){};
  window.bbAppendToolResult=function(raw){const r=typeof raw==="string"?JSON.parse(raw):raw;state.messages.push({role:"assistant",text:(field(r,"label","Label","Tool result")+": "+field(r,"message","Message",""))});render();};
  window.bbAppendScreenshotArtifact=function(raw){const a=typeof raw==="string"?JSON.parse(raw):raw;state.messages.push({kind:"screenshot",message:"Screenshot captured and stored locally.",path:a.path,fileName:a.fileName,capturedUtc:a.capturedUtc,width:a.width,height:a.height,thumbnailPath:a.thumbnailPath,localOnlyCloudState:a.localOnlyCloudState});render();};
  window.bbGetTranscript=function(){return state.messages.map(m=>({role:m.role||"assistant",content:m.text||m.message||""}));};
  render();
})();
