(function(){
  'use strict';

  var CONFIG=Object.assign({
    adsenseClient:'',
    autoAds:true,
    adSlots:{top:'',left:'',right:'',inline1:'',inline2:'',footer:''}
  },window.IMGSHOCK_CONFIG||{});

  var state={
    items:[],
    activeId:null,
    background:'transparent',
    rotation:0,
    flipX:false,
    addedCounter:0,
    exporting:false
  };

  var $=function(selector,context){return (context||document).querySelector(selector);};
  var $$=function(selector,context){return Array.prototype.slice.call((context||document).querySelectorAll(selector));};

  function escapeHTML(value){
    return String(value==null?'':value)
      .replace(/&/g,'&amp;')
      .replace(/</g,'&lt;')
      .replace(/>/g,'&gt;')
      .replace(/"/g,'&quot;')
      .replace(/'/g,'&#039;');
  }

  function uid(){
    if(window.crypto&&crypto.randomUUID)return crypto.randomUUID();
    return 'img-'+Date.now().toString(36)+'-'+Math.random().toString(36).slice(2);
  }

  function formatBytes(bytes){
    if(!Number.isFinite(bytes)||bytes<=0)return '0 B';
    var units=['B','KB','MB','GB'];
    var index=Math.min(Math.floor(Math.log(bytes)/Math.log(1024)),units.length-1);
    var value=bytes/Math.pow(1024,index);
    return (value>=10||index===0?value.toFixed(0):value.toFixed(1))+' '+units[index];
  }

  function baseName(name){
    return String(name||'image').replace(/\.[^.]+$/,'');
  }

  function extensionForMime(mime){
    if(mime==='image/jpeg')return 'jpg';
    if(mime==='image/webp')return 'webp';
    if(mime==='image/png')return 'png';
    return 'png';
  }

  function safeFilePart(value){
    return String(value||'image')
      .replace(/[<>:"/\\|?*\x00-\x1F]/g,'-')
      .replace(/\s+/g,' ')
      .trim()
      .replace(/[. ]+$/g,'')
      .slice(0,120)||'image';
  }

  function toast(title,message,type){
    var region=$('#toastRegion');
    var node=document.createElement('div');
    node.className='toast';
    node.innerHTML=
      '<span class="toast-icon">'+(type==='error'?'!':type==='success'?'✓':'•')+'</span>'+
      '<span class="toast-copy"><b>'+escapeHTML(title)+'</b><span>'+escapeHTML(message)+'</span></span>'+
      '<button class="toast-close" type="button" aria-label="Dismiss">×</button>';
    region.appendChild(node);
    requestAnimationFrame(function(){node.classList.add('show');});
    var close=function(){
      node.classList.remove('show');
      setTimeout(function(){if(node.parentNode)node.remove();},190);
    };
    $('.toast-close',node).addEventListener('click',close);
    setTimeout(close,4200);
  }

  function loadImage(url){
    return new Promise(function(resolve,reject){
      var image=new Image();
      image.onload=function(){resolve(image);};
      image.onerror=function(){reject(new Error('This image could not be decoded by your browser.'));};
      image.src=url;
    });
  }

  async function hashFile(file){
    if(!window.crypto||!crypto.subtle)return file.name+'-'+file.size+'-'+file.lastModified;
    try{
      var buffer=await file.arrayBuffer();
      var digest=await crypto.subtle.digest('SHA-256',buffer);
      return Array.from(new Uint8Array(digest)).map(function(byte){return byte.toString(16).padStart(2,'0');}).join('');
    }catch(error){
      return file.name+'-'+file.size+'-'+file.lastModified;
    }
  }

  async function inspectFile(file){
    if(!file||file.type.indexOf('image/')!==0)throw new Error('Only image files are supported.');
    var url=URL.createObjectURL(file);
    try{
      var image=await loadImage(url);
      return {
        id:uid(),
        file:file,
        url:url,
        name:file.name,
        size:file.size,
        type:file.type||'image/png',
        width:image.naturalWidth,
        height:image.naturalHeight,
        hash:await hashFile(file),
        duplicateOf:null,
        selected:true,
        addedAt:state.addedCounter++
      };
    }catch(error){
      URL.revokeObjectURL(url);
      throw error;
    }
  }

  async function addFiles(fileList){
    var files=Array.from(fileList||[]).filter(function(file){return file.type.indexOf('image/')===0;});
    if(!files.length){
      toast('No images found','Choose PNG, JPG, WebP, GIF or another browser-readable image.','error');
      return;
    }
    var rejected=(fileList?fileList.length:0)-files.length;
    var added=0;
    for(var i=0;i<files.length;i++){
      try{
        var item=await inspectFile(files[i]);
        state.items.push(item);
        added++;
        if(!state.activeId)state.activeId=item.id;
      }catch(error){
        console.warn(error);
      }
    }
    recomputeDuplicates();
    renderAll();
    toast('Images added',added+' image'+(added===1?'':'s')+' ready'+(rejected?' · '+rejected+' unsupported file'+(rejected===1?'':'s')+' skipped':''),'success');
  }

  function recomputeDuplicates(){
    var seen={};
    state.items.forEach(function(item){
      item.duplicateOf=seen[item.hash]||null;
      if(!seen[item.hash])seen[item.hash]=item.id;
    });
  }

  function activeItem(){
    return state.items.find(function(item){return item.id===state.activeId;})||null;
  }

  function selectedItems(){
    return state.items.filter(function(item){return item.selected;});
  }

  function filteredLibraryItems(){
    var query=$('#searchInput').value.trim().toLowerCase();
    var items=state.items.slice();
    if(query)items=items.filter(function(item){return item.name.toLowerCase().indexOf(query)!==-1;});
    return sortItems(items,$('#librarySort').value);
  }

  function sortItems(items,mode){
    var copy=items.slice();
    if(mode==='name')copy.sort(function(a,b){return a.name.localeCompare(b.name);});
    if(mode==='size-desc')copy.sort(function(a,b){return b.size-a.size;});
    if(mode==='size-asc')copy.sort(function(a,b){return a.size-b.size;});
    if(mode==='pixels-desc')copy.sort(function(a,b){return (b.width*b.height)-(a.width*a.height);});
    if(mode==='added'||mode==='library')copy.sort(function(a,b){return a.addedAt-b.addedAt;});
    return copy;
  }

  function renderAll(){
    renderStats();
    renderLibrary();
    renderPreview();
    renderOrganise();
    renderExportSummary();
    $('#editorShell').classList.toggle('hidden',!state.items.length);
    $('#workspaceStats').classList.toggle('hidden',!state.items.length);
  }

  function renderStats(){
    var total=state.items.reduce(function(sum,item){return sum+item.size;},0);
    var duplicates=state.items.filter(function(item){return !!item.duplicateOf;}).length;
    $('#statCount').textContent=state.items.length;
    $('#statSize').textContent=formatBytes(total);
    $('#statDuplicates').textContent=duplicates;
    $('#statSelected').textContent=selectedItems().length;
  }

  function renderLibrary(){
    var host=$('#imageGrid');
    var items=filteredLibraryItems();
    if(!state.items.length){
      host.innerHTML='';
      return;
    }
    if(!items.length){
      host.innerHTML='<div class="library-empty">No images match that search.</div>';
      return;
    }
    host.innerHTML=items.map(function(item){
      return '<article class="image-card '+(item.id===state.activeId?'active':'')+'" data-image-id="'+item.id+'">'+
        '<label class="card-check" title="Select for export"><input type="checkbox" data-select-id="'+item.id+'" '+(item.selected?'checked':'')+'><i></i></label>'+
        '<div class="thumb"><img src="'+item.url+'" alt="">'+(item.duplicateOf?'<span class="duplicate-badge">Duplicate</span>':'')+'</div>'+
        '<div class="card-body"><b>'+escapeHTML(item.name)+'</b><span>'+item.width+'×'+item.height+' · '+formatBytes(item.size)+'</span></div>'+
      '</article>';
    }).join('');

    $$('[data-image-id]',host).forEach(function(card){
      card.addEventListener('click',function(event){
        if(event.target.closest('.card-check'))return;
        state.activeId=card.getAttribute('data-image-id');
        renderLibrary();
        renderPreview();
        updateDownloadButton();
      });
    });

    $$('[data-select-id]',host).forEach(function(input){
      input.addEventListener('change',function(){
        var item=state.items.find(function(candidate){return candidate.id===input.getAttribute('data-select-id');});
        if(item)item.selected=input.checked;
        renderStats();
        renderExportSummary();
      });
    });
  }

  function renderPreview(){
    var item=activeItem();
    var stage=$('#previewStage');
    if(!item){
      $('#previewName').textContent='Choose an image';
      $('#previewMeta').textContent='—';
      stage.innerHTML='<div class="preview-empty"><span class="empty-symbol"><svg class="icon icon-lg" viewBox="0 0 24 24"><rect x="4" y="5" width="16" height="14" rx="3"></rect><path d="m7 15 3-3 2.2 2.2 1.8-1.8L17 15"></path></svg></span><p>Select a thumbnail to preview it.</p></div>';
      updateDownloadButton();
      return;
    }
    $('#previewName').textContent=item.name;
    $('#previewMeta').textContent=item.width+'×'+item.height+' · '+formatBytes(item.size);
    stage.innerHTML='<img src="'+item.url+'" alt="'+escapeHTML(item.name)+'">';
    updateDownloadButton();
  }

  function orientationName(item){
    var ratio=item.width/item.height;
    if(Math.abs(ratio-1)<.06)return 'Square';
    return ratio>1?'Landscape':'Portrait';
  }

  function resolutionBucket(item){
    var mp=(item.width*item.height)/1000000;
    if(mp<1)return 'Small';
    if(mp<4)return 'Medium';
    if(mp<12)return 'Large';
    return 'Huge';
  }

  function targetMime(item){
    var requested=$('#formatSelect').value;
    if(requested!=='original')return requested;
    if(['image/png','image/jpeg','image/webp'].indexOf(item.type)!==-1)return item.type;
    return 'image/png';
  }

  function targetFolder(item){
    var mode=$('#groupSelect').value;
    if(mode==='orientation')return orientationName(item);
    if(mode==='format')return extensionForMime(targetMime(item)).toUpperCase();
    if(mode==='resolution')return resolutionBucket(item);
    return '';
  }

  function outputOrder(){
    var items=$('#selectedOnlyCheckbox').checked?selectedItems():state.items.slice();
    var mode=$('#exportSort').value;
    if(mode==='library')return sortItems(items,'added');
    return sortItems(items,mode);
  }

  function outputName(item,index){
    var pattern=$('#renamePattern').value.trim()||'{name}';
    var ext=extensionForMime(targetMime(item));
    var replacements={
      '{index}':String(index+1).padStart(3,'0'),
      '{name}':baseName(item.name),
      '{width}':String(item.width),
      '{height}':String(item.height),
      '{type}':ext
    };
    Object.keys(replacements).forEach(function(token){
      pattern=pattern.split(token).join(replacements[token]);
    });
    pattern=safeFilePart(pattern);
    return pattern+'.'+ext;
  }

  function renderOrganise(){
    var first=outputOrder()[0];
    $('#renameExample').textContent='Example: '+(first?outputName(first,0):'image-001.webp');
    var folderHost=$('#folderPreview');
    if(!state.items.length){
      folderHost.innerHTML='<div class="folder-preview-row"><span>No images yet</span><b>0</b></div>';
      return;
    }
    var counts={};
    outputOrder().forEach(function(item){
      var folder=targetFolder(item)||'Root';
      counts[folder]=(counts[folder]||0)+1;
    });
    folderHost.innerHTML=Object.keys(counts).sort().map(function(folder){
      return '<div class="folder-preview-row"><span>'+escapeHTML(folder)+'</span><b>'+counts[folder]+'</b></div>';
    }).join('');
  }

  function renderExportSummary(){
    var items=outputOrder();
    var total=items.reduce(function(sum,item){return sum+item.size;},0);
    $('#exportSummary').textContent=items.length
      ?items.length+' image'+(items.length===1?'':'s')+' ready · '+formatBytes(total)+' before processing.'
      :'Add images to start building your export.';
    $('#exportZipButton').disabled=!items.length||state.exporting;
    $('#exportStatus').textContent=state.exporting?'Processing images…':items.length?'Ready to export':'Nothing to export yet';
    updateDownloadButton();
  }

  function updateDownloadButton(){
    $('#downloadCurrentButton').disabled=!activeItem()||state.exporting;
  }

  function currentSettings(){
    var maxWidth=parseInt($('#maxWidthInput').value,10);
    var maxHeight=parseInt($('#maxHeightInput').value,10);
    return {
      mime:null,
      quality:Math.max(.35,Math.min(1,parseInt($('#qualityRange').value,10)/100)),
      maxWidth:Number.isFinite(maxWidth)&&maxWidth>0?maxWidth:null,
      maxHeight:Number.isFinite(maxHeight)&&maxHeight>0?maxHeight:null,
      padding:parseInt($('#paddingRange').value,10)||0,
      radius:parseInt($('#radiusRange').value,10)||0,
      background:state.background,
      rotation:((state.rotation%360)+360)%360,
      flipX:state.flipX
    };
  }

  function roundRectPath(ctx,x,y,width,height,radius){
    var r=Math.max(0,Math.min(radius,Math.min(width,height)/2));
    ctx.beginPath();
    ctx.moveTo(x+r,y);
    ctx.arcTo(x+width,y,x+width,y+height,r);
    ctx.arcTo(x+width,y+height,x,y+height,r);
    ctx.arcTo(x,y+height,x,y,r);
    ctx.arcTo(x,y,x+width,y,r);
    ctx.closePath();
  }

  async function processItem(item){
    var image=await loadImage(item.url);
    var settings=currentSettings();
    var rotation=settings.rotation;
    var swapped=rotation===90||rotation===270;
    var rotatedWidth=swapped?item.height:item.width;
    var rotatedHeight=swapped?item.width:item.height;
    var scale=1;
    if(settings.maxWidth)scale=Math.min(scale,settings.maxWidth/rotatedWidth);
    if(settings.maxHeight)scale=Math.min(scale,settings.maxHeight/rotatedHeight);
    scale=Math.min(1,scale);
    var displayWidth=Math.max(1,Math.round(rotatedWidth*scale));
    var displayHeight=Math.max(1,Math.round(rotatedHeight*scale));
    var pad=settings.padding;
    var canvas=document.createElement('canvas');
    canvas.width=displayWidth+pad*2;
    canvas.height=displayHeight+pad*2;
    var ctx=canvas.getContext('2d',{alpha:true});
    if(!ctx)throw new Error('Canvas processing is unavailable.');

    if(settings.background!=='transparent'){
      ctx.fillStyle=settings.background;
      ctx.fillRect(0,0,canvas.width,canvas.height);
    }

    ctx.save();
    roundRectPath(ctx,pad,pad,displayWidth,displayHeight,settings.radius);
    ctx.clip();

    ctx.translate(pad+displayWidth/2,pad+displayHeight/2);
    if(settings.flipX)ctx.scale(-1,1);
    ctx.rotate(rotation*Math.PI/180);
    var drawWidth=item.width*scale;
    var drawHeight=item.height*scale;
    ctx.drawImage(image,-drawWidth/2,-drawHeight/2,drawWidth,drawHeight);
    ctx.restore();

    var mime=targetMime(item);
    var blob=await new Promise(function(resolve,reject){
      canvas.toBlob(function(result){
        if(result)resolve(result);
        else reject(new Error('The browser could not encode this image.'));
      },mime,settings.quality);
    });

    return {blob:blob,width:canvas.width,height:canvas.height,mime:mime};
  }

  function downloadBlob(blob,filename){
    var url=URL.createObjectURL(blob);
    var link=document.createElement('a');
    link.href=url;
    link.download=filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(function(){URL.revokeObjectURL(url);},1500);
  }

  async function downloadCurrent(){
    var item=activeItem();
    if(!item)return;
    try{
      setBusy(true);
      var processed=await processItem(item);
      var index=Math.max(0,outputOrder().findIndex(function(candidate){return candidate.id===item.id;}));
      downloadBlob(processed.blob,outputName(item,index));
      toast('Image exported','Processed locally and downloaded.','success');
    }catch(error){
      toast('Export failed',error.message,'error');
    }finally{
      setBusy(false);
    }
  }

  async function exportZip(){
    var items=outputOrder();
    if(!items.length)return;
    if(!window.JSZip){
      toast('ZIP library unavailable','Reload the page while connected to the internet, then try again.','error');
      return;
    }
    try{
      setBusy(true);
      var zip=new JSZip();
      for(var i=0;i<items.length;i++){
        $('#exportStatus').textContent='Processing '+(i+1)+' of '+items.length+'…';
        var item=items[i];
        var processed=await processItem(item);
        var folder=targetFolder(item);
        var path=(folder?safeFilePart(folder)+'/':'')+outputName(item,i);
        zip.file(path,processed.blob);
      }
      $('#exportStatus').textContent='Building ZIP…';
      var archive=await zip.generateAsync({type:'blob',compression:'DEFLATE',compressionOptions:{level:6}});
      downloadBlob(archive,'imgshock-export.zip');
      toast('ZIP ready',items.length+' processed image'+(items.length===1?'':'s')+' downloaded.','success');
    }catch(error){
      console.error(error);
      toast('ZIP export failed',error.message,'error');
    }finally{
      setBusy(false);
    }
  }

  function setBusy(busy){
    state.exporting=busy;
    $('#exportZipButton').disabled=busy||!outputOrder().length;
    $('#downloadCurrentButton').disabled=busy||!activeItem();
    renderExportSummary();
  }

  function removeDuplicates(){
    var before=state.items.length;
    var idsToRemove=new Set(state.items.filter(function(item){return !!item.duplicateOf;}).map(function(item){return item.id;}));
    if(!idsToRemove.size){
      toast('No duplicates','Exact-file duplicate detection found nothing to remove.','info');
      return;
    }
    state.items=state.items.filter(function(item){
      if(idsToRemove.has(item.id)){
        URL.revokeObjectURL(item.url);
        return false;
      }
      return true;
    });
    if(idsToRemove.has(state.activeId))state.activeId=state.items[0]?state.items[0].id:null;
    recomputeDuplicates();
    renderAll();
    toast('Duplicates removed',(before-state.items.length)+' duplicate'+((before-state.items.length)===1?'':'s')+' removed.','success');
  }

  function clearAll(){
    state.items.forEach(function(item){URL.revokeObjectURL(item.url);});
    state.items=[];
    state.activeId=null;
    renderAll();
    toast('Workspace cleared','Your local image list is empty.','info');
  }

  function resetEdits(){
    $('#formatSelect').value='original';
    $('#qualityRange').value='88';
    $('#maxWidthInput').value='';
    $('#maxHeightInput').value='';
    $('#paddingRange').value='0';
    $('#radiusRange').value='0';
    state.background='transparent';
    state.rotation=0;
    state.flipX=false;
    $$$('.swatch').forEach(function(button){button.classList.toggle('active',button.getAttribute('data-bg')==='transparent');});
    updateSettingLabels();
    renderOrganise();
    toast('Edits reset','Batch processing settings returned to defaults.','info');
  }

  function updateSettingLabels(){
    $('#qualityValue').textContent=$('#qualityRange').value+'%';
    $('#paddingValue').textContent=$('#paddingRange').value+'px';
    $('#radiusValue').textContent=$('#radiusRange').value+'px';
  }

  function initAds(){
    var client=String(CONFIG.adsenseClient||'').trim();
    if(!client)return;
    if(!document.querySelector('script[data-imgshock-ads]')){
      var script=document.createElement('script');
      script.async=true;
      script.crossOrigin='anonymous';
      script.dataset.imgshockAds='1';
      script.src='https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client='+encodeURIComponent(client);
      document.head.appendChild(script);
    }

    $$$('[data-ad-key]').forEach(function(container){
      var key=container.getAttribute('data-ad-key');
      var slot=CONFIG.adSlots&&String(CONFIG.adSlots[key]||'').trim();
      if(!slot){
        if(CONFIG.autoAds)container.classList.add('hidden');
        return;
      }
      container.innerHTML='<span class="ad-label">Advertisement</span>';
      var ad=document.createElement('ins');
      ad.className='adsbygoogle';
      ad.style.display='block';
      ad.setAttribute('data-ad-client',client);
      ad.setAttribute('data-ad-slot',slot);
      ad.setAttribute('data-ad-format','auto');
      ad.setAttribute('data-full-width-responsive','true');
      container.appendChild(ad);
      try{(window.adsbygoogle=window.adsbygoogle||[]).push({});}catch(error){console.warn('Ad slot could not initialise',key,error);}
    });
  }


  var THEME_KEY='imgshock-theme-choice';
  var darkQuery=window.matchMedia('(prefers-color-scheme: dark)');

  function resolveTheme(choice){
    return choice==='system'?(darkQuery.matches?'dark':'light'):choice;
  }

  function applyTheme(choice){
    var resolved=resolveTheme(choice);
    document.documentElement.dataset.theme=resolved;
    var meta=document.querySelector('meta[name="theme-color"]');
    if(meta)meta.setAttribute('content',resolved==='dark'?'#060808':'#f3f5f1');
    $$('[data-theme-choice]').forEach(function(button){
      button.classList.toggle('active',button.getAttribute('data-theme-choice')===choice);
    });
    requestAnimationFrame(refreshLiquids);
  }

  function setThemeChoice(choice){
    if(['light','dark','system'].indexOf(choice)===-1)choice='system';
    localStorage.setItem(THEME_KEY,choice);
    applyTheme(choice);
  }

  function openAppearance(){
    var modal=$('#appearanceModal');
    if(!modal)return;
    modal.classList.remove('hidden');
    modal.setAttribute('aria-hidden','false');
    requestAnimationFrame(refreshLiquids);
  }

  function closeAppearance(){
    var modal=$('#appearanceModal');
    if(!modal)return;
    modal.classList.add('hidden');
    modal.setAttribute('aria-hidden','true');
  }

  function ensureLiquid(container){
    if(!container||container.dataset.liquidReady)return;
    container.dataset.liquidReady='1';
    var goo=document.createElement('span');
    goo.className='liquid-goo';
    goo.innerHTML='<span class="liquid-bridge"></span><span class="liquid-blob"></span>';
    var indicator=document.createElement('span');
    indicator.className='liquid-indicator';
    container.prepend(indicator);
    container.prepend(goo);
  }

  function positionLiquid(container,active,animate){
    if(!container||!active)return;
    if(animate===undefined)animate=true;
    ensureLiquid(container);
    var indicator=$('.liquid-indicator',container);
    var blob=$('.liquid-blob',container);
    var bridge=$('.liquid-bridge',container);
    var c=container.getBoundingClientRect();
    var r=active.getBoundingClientRect();
    var x=r.left-c.left;
    var width=r.width;
    var oldX=Number(indicator.dataset.x||x);
    var oldWidth=Number(indicator.dataset.w||width);
    var oldCenter=oldX+oldWidth/2;
    var newCenter=x+width/2;
    var left=Math.min(oldCenter,newCenter);
    var right=Math.max(oldCenter,newCenter);

    blob.style.left=x+'px';
    blob.style.width=width+'px';
    bridge.style.left=(left-8)+'px';
    bridge.style.width=(right-left+16)+'px';
    indicator.style.width=width+'px';
    indicator.style.transform='translateX('+x+'px)';
    indicator.dataset.x=String(x);
    indicator.dataset.w=String(width);

    if(animate&&!window.matchMedia('(prefers-reduced-motion: reduce)').matches&&Math.abs(newCenter-oldCenter)>2){
      indicator.classList.remove('moving');
      bridge.classList.remove('animate');
      void indicator.offsetWidth;
      indicator.classList.add('moving');
      bridge.classList.add('animate');
      clearTimeout(container._liquidTimer);
      container._liquidTimer=setTimeout(function(){
        indicator.classList.remove('moving');
        bridge.classList.remove('animate');
      },370);
    }
  }

  function refreshLiquids(){
    $$('[data-liquid-tabs]').forEach(function(container){
      ensureLiquid(container);
      var active=$('.active',container)||$('a,button',container);
      if(active)positionLiquid(container,active,false);
    });
  }

  function setNavigationActive(id,animate){
    $$('[data-nav-target]').forEach(function(control){
      control.classList.toggle('active',control.getAttribute('data-nav-target')===id);
    });
    ['desktopTabs','mobileTabs'].forEach(function(tabId){
      var container=$('#'+tabId);
      if(!container)return;
      var active=$('[data-nav-target="'+id+'"]',container);
      if(active)positionLiquid(container,active,animate!==false);
    });
  }

  function initNavigation(){
    $$('[data-nav-target]').forEach(function(control){
      control.addEventListener('click',function(){
        setNavigationActive(control.getAttribute('data-nav-target'),true);
      });
    });

    var sections=['workspace','organise','export'].map(function(id){return document.getElementById(id);}).filter(Boolean);
    if('IntersectionObserver' in window){
      var scores={};
      var observer=new IntersectionObserver(function(entries){
        entries.forEach(function(entry){scores[entry.target.id]=entry.isIntersecting?entry.intersectionRatio:0;});
        var best='workspace';
        var bestScore=-1;
        Object.keys(scores).forEach(function(id){
          if(scores[id]>bestScore){best=id;bestScore=scores[id];}
        });
        if(bestScore>0)setNavigationActive(best,true);
      },{threshold:[.18,.35,.55],rootMargin:'-24% 0px -52% 0px'});
      sections.forEach(function(section){observer.observe(section);});
    }

    window.addEventListener('resize',refreshLiquids,{passive:true});
    requestAnimationFrame(refreshLiquids);
  }

  function initReveals(){
    var nodes=$('.reveal');
    if(window.matchMedia('(prefers-reduced-motion: reduce)').matches||!('IntersectionObserver' in window)){
      nodes.forEach(function(node){node.classList.add('is-visible');});
      return;
    }
    var observer=new IntersectionObserver(function(entries){
      entries.forEach(function(entry){
        entry.target.classList.toggle('is-visible',entry.isIntersecting);
      });
    },{threshold:.12,rootMargin:'-3% 0px -7% 0px'});
    nodes.forEach(function(node){observer.observe(node);});
  }

  function initInterface(){
    var stored=localStorage.getItem(THEME_KEY)||'system';
    applyTheme(stored);

    if(darkQuery.addEventListener){
      darkQuery.addEventListener('change',function(){
        if((localStorage.getItem(THEME_KEY)||'system')==='system')applyTheme('system');
      });
    }

    $$('[data-theme-choice]').forEach(function(button){
      button.addEventListener('click',function(){
        setThemeChoice(button.getAttribute('data-theme-choice'));
        positionLiquid($('#themeTabs'),button,true);
      });
    });

    [$('#settingsOpen'),$('#settingsOpenMobile')].filter(Boolean).forEach(function(button){
      button.addEventListener('click',openAppearance);
    });
    [$('#settingsClose'),$('#settingsBackdrop')].filter(Boolean).forEach(function(button){
      button.addEventListener('click',closeAppearance);
    });
    document.addEventListener('keydown',function(event){
      if(event.key==='Escape')closeAppearance();
    });

    initNavigation();
    initReveals();
    requestAnimationFrame(function(){
      refreshLiquids();
      var themeButton=$('[data-theme-choice="'+stored+'"]');
      if(themeButton)positionLiquid($('#themeTabs'),themeButton,false);
    });
  }

  function bind(){
    var dropZone=$('#dropZone');
    var input=$('#fileInput');

    dropZone.addEventListener('click',function(){input.click();});
    dropZone.addEventListener('keydown',function(event){
      if(event.key==='Enter'||event.key===' '){event.preventDefault();input.click();}
    });
    input.addEventListener('change',function(){
      addFiles(input.files);
      input.value='';
    });
    ['dragenter','dragover'].forEach(function(type){
      dropZone.addEventListener(type,function(event){event.preventDefault();dropZone.classList.add('dragging');});
    });
    ['dragleave','drop'].forEach(function(type){
      dropZone.addEventListener(type,function(event){event.preventDefault();dropZone.classList.remove('dragging');});
    });
    dropZone.addEventListener('drop',function(event){
      if(event.dataTransfer&&event.dataTransfer.files)addFiles(event.dataTransfer.files);
    });

    document.addEventListener('paste',function(event){
      var clipboard=event.clipboardData;
      if(!clipboard||!clipboard.items)return;

      var imageFiles=[];
      Array.from(clipboard.items).forEach(function(item,index){
        if(item.kind!=='file'||item.type.indexOf('image/')!==0)return;
        var file=item.getAsFile();
        if(!file)return;

        if(!file.name||file.name==='image.png'){
          var ext=extensionForMime(file.type||'image/png');
          try{
            file=new File(
              [file],
              'pasted-image-'+new Date().toISOString().replace(/[:.]/g,'-')+'-'+(index+1)+'.'+ext,
              {type:file.type||'image/png',lastModified:Date.now()}
            );
          }catch(error){
            // Older browsers can still use the original clipboard File.
          }
        }
        imageFiles.push(file);
      });

      if(!imageFiles.length)return;
      event.preventDefault();
      dropZone.classList.add('dragging');
      setTimeout(function(){dropZone.classList.remove('dragging');},240);
      addFiles(imageFiles);
      toast(
        'Pasted from clipboard',
        imageFiles.length+' image'+(imageFiles.length===1?'':'s')+' added to your workspace.',
        'success'
      );
    });

    $('#searchInput').addEventListener('input',renderLibrary);
    $('#librarySort').addEventListener('change',renderLibrary);

    $('#selectAllButton').addEventListener('click',function(){
      var shouldSelect=selectedItems().length!==state.items.length;
      state.items.forEach(function(item){item.selected=shouldSelect;});
      renderAll();
    });
    $('#clearButton').addEventListener('click',clearAll);
    $('#removeDuplicatesButton').addEventListener('click',removeDuplicates);

    ['qualityRange','paddingRange','radiusRange'].forEach(function(id){
      $('#'+id).addEventListener('input',function(){updateSettingLabels();renderOrganise();});
    });
    ['formatSelect','maxWidthInput','maxHeightInput'].forEach(function(id){
      $('#'+id).addEventListener('change',function(){renderOrganise();renderExportSummary();});
    });

    $$$('.swatch').forEach(function(button){
      button.addEventListener('click',function(){
        state.background=button.getAttribute('data-bg');
        $$$('.swatch').forEach(function(candidate){candidate.classList.toggle('active',candidate===button);});
      });
    });
    $('#customColorInput').addEventListener('input',function(){
      state.background=$('#customColorInput').value;
      $$$('.swatch').forEach(function(candidate){candidate.classList.remove('active');});
    });

    $$$('[data-rotate]').forEach(function(button){
      button.addEventListener('click',function(){
        state.rotation=(state.rotation+parseInt(button.getAttribute('data-rotate'),10)+360)%360;
        toast('Rotation set',state.rotation+'° will be applied during export.','info');
      });
    });
    $('#flipXButton').addEventListener('click',function(){
      state.flipX=!state.flipX;
      $('#flipXButton').classList.toggle('active',state.flipX);
      toast('Horizontal flip',state.flipX?'Enabled for export.':'Disabled.','info');
    });
    $('#resetEditsButton').addEventListener('click',resetEdits);

    $('#renamePattern').addEventListener('input',function(){renderOrganise();renderExportSummary();});
    $$$('[data-token]').forEach(function(button){
      button.addEventListener('click',function(){
        var field=$('#renamePattern');
        var token=button.getAttribute('data-token');
        var start=field.selectionStart==null?field.value.length:field.selectionStart;
        var end=field.selectionEnd==null?field.value.length:field.selectionEnd;
        field.value=field.value.slice(0,start)+token+field.value.slice(end);
        field.focus();
        field.setSelectionRange(start+token.length,start+token.length);
        renderOrganise();
      });
    });
    $('#groupSelect').addEventListener('change',renderOrganise);
    $('#exportSort').addEventListener('change',function(){renderOrganise();renderExportSummary();});
    $('#selectedOnlyCheckbox').addEventListener('change',function(){renderOrganise();renderExportSummary();});

    $('#downloadCurrentButton').addEventListener('click',downloadCurrent);
    $('#exportZipButton').addEventListener('click',exportZip);
  }

  function init(){
    bind();
    initInterface();
    updateSettingLabels();
    renderAll();
    initAds();
  }

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init);
  else init();
})();