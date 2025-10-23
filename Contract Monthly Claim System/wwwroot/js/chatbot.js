(function(){
  function qs(sel, root=document){ return root.querySelector(sel); }
  function qsa(sel, root=document){ return Array.from(root.querySelectorAll(sel)); }
  function el(tag, cls){ const e=document.createElement(tag); if(cls) e.className=cls; return e; }

  function addMsg(role, text){
    const body = qs('#cmcs-chat-body'); if(!body) return;
    const wrap = el('div', 'cmcs-chat-msg ' + (role==='user'?'user':'bot'));
    const bubble = el('div', 'cmcs-chat-bubble'); bubble.textContent = text; wrap.appendChild(bubble);
    body.appendChild(wrap); body.scrollTop = body.scrollHeight;
  }
  function addTyping(){
    const body = qs('#cmcs-chat-body'); if(!body) return null;
    const wrap = el('div', 'cmcs-chat-msg bot');
    const bubble = el('div', 'cmcs-chat-bubble');
    const d1 = el('span','cmcs-typing'); const d2 = el('span','cmcs-typing'); const d3 = el('span','cmcs-typing');
    bubble.appendChild(d1); bubble.appendChild(d2); bubble.appendChild(d3);
    wrap.appendChild(bubble); body.appendChild(wrap); body.scrollTop = body.scrollHeight;
    return wrap;
  }
  function removeTyping(node){ if(node && node.parentNode) node.parentNode.removeChild(node); }

  function suggestionsFor(role){
    if(role==='manager') return [
      'How can I view financial reports?',
      'Show me all rejected claims this month.',
      'What is the total processed amount?',
      'How to oversee claim activities?'
    ];
    if(role==='coordinator') return [
      'How can I approve a claim?',
      'Where do I view pending claims?',
      'How do I reject a claim?',
      'How do I contact a lecturer?'
    ];
    return [
      'How do I submit a new claim?',
      'Where can I upload my documents?',
      'How do I check if my claim was approved?'
    ];
  }

  function answerFor(role, msg){
    const m = (msg||'').toLowerCase().trim();
    if(!m) return {
      tone: role,
      text: 'Please type a question. For example: ' + suggestionsFor(role).join(' | ')
    };
    if(m==='help'){
      if(role==='manager') return { tone: role, text: 'I can help with: reports overview, rejected/approved stats, and financial totals. Try: "How can I view financial reports?"'};
      if(role==='coordinator') return { tone: role, text: 'I can help with: reviewing claims, approving/rejecting, and finding pending claims. Try: "Where do I view pending claims?"'};
      return { tone: role, text: 'I can help with: submitting claims, uploading documents, and tracking status. Try: "How do I submit a new claim?"'};
    }

    // Simple intent matching
    const intent = {
      submit: /(submit|new claim|create claim|make.+claim)/,
      upload: /(upload|document|file|evidence|supporting)/,
      status: /(status|approved|rejected|pending|track|progress)/,
      approve: /(approve|approval)/,
      reject: /(reject|rejection)/,
      pending: /(pending|review)/,
      report: /(report|financial|summary|analytics)/,
      rejectedMonth: /(rejected).*(month)|month.*(rejected)/,
      total: /(total processed|total amount|sum|aggregate)/,
      oversee: /(oversee|overview|monitor|manage)/
    };

    function forLecturer(){
      if(intent.submit.test(m)) return 'To submit: open Lecturer Dashboard → "Submit Claim" → fill Title, Date, Hours, Rate, Category, Notes → add documents → Submit. Total auto-calculates. You will see it under Recent Claims.';
      if(intent.upload.test(m)) return 'In the submit form, use the Supporting Documents section. Click the upload area or drag-and-drop PDF/DOCX/XLSX (≤10MB). Multiple files are supported.';
      if(intent.status.test(m)) return 'Track status on your Lecturer Dashboard under Recent Claims. Status will update in real time (Pending/Approved/Rejected).';
      return 'I can help you submit claims, upload documents, and track status. Try: "How do I submit a new claim?"';
    }
    function forCoordinator(){
      if(intent.pending.test(m)) return 'Go to Review Claims or Coordinator Dashboard → the table shows all claims. Filter by status Pending to see items needing review.';
      if(intent.approve.test(m)) return 'In the claims table, use the Approve button per row. It updates instantly and notifies the lecturer.';
      if(intent.reject.test(m)) return 'Use the Reject button on the row. Optionally leave notes in the claim before rejecting to give context to the lecturer.';
      return 'I can help you review, approve or reject claims. Try: "Where do I view pending claims?"';
    }
    function forManager(){
      if(intent.report.test(m)) return 'On the Manager Dashboard, use the overview and totals. For detailed financial reports, export data from Review Claims and apply date/status filters.';
      if(intent.rejectedMonth.test(m)) return 'Open Review Claims → filter Status = Rejected and Date = This Month. The table shows all rejected claims this month.';
      if(intent.total.test(m)) return 'The dashboard shows Total Processed (sum of Approved claims). For a custom period, filter in Review Claims and calculate or export.';
      if(intent.oversee.test(m)) return 'Use Manager Dashboard for KPIs and Review Claims for per-claim actions. You can also switch views to Coordinator/Lecturer to inspect details.';
      return 'I can help with oversight, reports, and financial summaries. Try: "How can I view financial reports?"';
    }

    let text;
    if(role==='manager') text = forManager();
    else if(role==='coordinator') text = forCoordinator();
    else text = forLecturer();

    return { tone: role, text };
  }

  function init(){
    const root = qs('#cmcs-chatbot'); if(!root) return;
    const role = root.getAttribute('data-role') || 'lecturer';
    const toggle = qs('#cmcs-chatbot-toggle');
    const win = qs('.cmcs-chat-window');
    const closeBtn = qs('.cmcs-chat-close');
    const input = qs('#cmcs-chat-input');
    const send = qs('#cmcs-chat-send');
    const attach = qs('#cmcs-chat-attach');
    const fileInput = qs('#cmcs-chat-file');
    const sugg = qs('#cmcs-chat-suggestions');

    function open(){ win.classList.add('open'); input && input.focus(); }
    function close(){ win.classList.remove('open'); }

    toggle && toggle.addEventListener('click', open);
    closeBtn && closeBtn.addEventListener('click', close);

    // Populate suggestions
    sugg.innerHTML = '';
    suggestionsFor(role).forEach(s => {
      const chip = el('button','cmcs-suggestion'); chip.type='button'; chip.textContent = s; chip.addEventListener('click',()=>sendMsg(s)); sugg.appendChild(chip);
    });

    async function sendMsg(message){
      if(!message && (!fileInput || !fileInput.files || fileInput.files.length===0)){
        return;
      }
      if(message){ addMsg('user', message); }
      else { addMsg('user', '[Uploaded documents]'); }
      const typing = addTyping();

      // If files present or message looks complex, hit server Chat/Ask; else local canned
      const looksComplex = /\b(total|highest|lowest|rejected|approved|this month|last month|between|report|sum)\b/i.test(message||'');
      const hasFiles = fileInput && fileInput.files && fileInput.files.length>0;
      if(hasFiles || looksComplex){
        try{
          const fd = new FormData();
          fd.append('message', message||'');
          if(hasFiles){
            Array.from(fileInput.files).forEach(f=> fd.append('files', f, f.name));
          }
          const resp = await fetch('/Chat/Ask', { method: 'POST', body: fd, credentials: 'include' });
          const data = await resp.json();
          removeTyping(typing);
          addMsg('bot', data && data.text ? data.text : 'No response.');
          if(fileInput) fileInput.value='';
          return;
        }catch(err){
          removeTyping(typing);
          addMsg('bot', 'Server error. Please try again.');
          console.error('Chat/Ask error', err);
          return;
        }
      }

      // Local canned fallback
      try{
        const ans = answerFor(role, message);
        removeTyping(typing);
        addMsg('bot', ans.text);
      }catch(err){
        removeTyping(typing);
        addMsg('bot', 'Sorry, I ran into a problem understanding that. Please try again or type "help".');
        console.error('Chatbot error:', err);
      }
    }

    send && send.addEventListener('click', ()=>{ const v=input.value.trim(); if(!v && (!fileInput || !fileInput.files || fileInput.files.length===0)){ return; } input.value=''; sendMsg(v); });
    input && input.addEventListener('keydown', (e)=>{ if(e.key==='Enter'){ e.preventDefault(); const v=input.value.trim(); if(!v) return; input.value=''; sendMsg(v); }});
    attach && attach.addEventListener('click', ()=>{ fileInput && fileInput.click(); });

    // Greet based on role
    const greet = role==='manager' ? 'Hello. I can assist with oversight, reports, and financial summaries.' : role==='coordinator' ? 'Hello. I can help you review, approve, or reject claims.' : 'Hi! I can help you submit claims, upload documents, and track status.';
    addMsg('bot', greet + ' Type "help" or tap a suggestion below.');
  }

  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
