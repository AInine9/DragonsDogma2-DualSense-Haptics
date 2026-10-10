-- DD2 DualSense bridge 0.1.0. Audio is untouched; the companion plays locally
-- prepared tactile WAVs. No fabricated rumble pattern is used for unknown sounds.
local STATE = "dd2_dualsense_state.json"
local CONTROL = "dd2_dualsense_control.json"
local session = tostring(os.time()) .. ":" .. tostring(os.clock())
local frame, snapshot, event_seq = 0, 0, 0
local published_event_seq, published_suppressed = 0, false
local published_frame=0
local events, owned_ids, switches, states = {}, {}, {}, {}
local lifetimes, lifetime_count, playing_method = {}, 0, nil
local lifetime_recent={}
local enabled, hooks_ready, suppressed = true, false, false
local host, player_go, saved_device, saved_feedback = nil, nil, nil, nil
local player_character
local hybrid_device=nil
local player_wait_reason=""
local error_text, last_event = "", "none"
local routes, id_method
local counts = {posted=0, matched=0, foreign=0, dropped=0, switch_updates=0, output_observed={}, output_blocked={}}
local recent = {}
local nearby_positions,nearby_count={},0
local function forget_nearby(id)
    if nearby_positions[id] then
        nearby_positions[id]=nil
        nearby_count=nearby_count-1
    end
end
local function update_nearby(id,x,y,z,at)
    if x==nil then forget_nearby(id);return end
    local p=nearby_positions[id]
    if not p then
        if nearby_count>=512 then return end
        p={};nearby_positions[id]=p;nearby_count=nearby_count+1
    end
    -- Keep every coordinate read fresh, but reuse the row for a registered ID.
    p.x,p.y,p.z,p.frame=x,y,z,at
end
local function sound_position(go)
    local t=go and go:call("get_Transform")
    local p=t and t:call("get_Position")
    if not p then return nil end
    local x,y,z=p.x,p.y,p.z
    if type(x)~="number" or type(y)~="number" or type(z)~="number" then return nil end
    if x~=x or y~=y or z~=z or math.abs(x)>1000000 or math.abs(y)>1000000 or math.abs(z)>1000000 then return nil end
    return x,y,z
end
local function nearby_level(id,object)
    local rule=routes and routes.nearby_events and routes.nearby_events[tostring(id)]
    local p=nearby_positions[object]
    if not rule or not p or frame-p.frame>2 then return nil end
    if type(rule.radius)~="number" or rule.radius~=rule.radius or rule.radius<=5 or rule.radius>100
        or type(rule.gain)~="number" or rule.gain~=rule.gain or rule.gain<=0 or rule.gain>1.5 then return nil end
    local px,py,pz=p.x,p.y,p.z -- Preserve this sample across the player's SDK calls.
    local ok,x,y,z=pcall(sound_position,player_go)
    if not ok or x==nil then return nil end
    local distance=math.sqrt((px-x)^2+(py-y)^2+(pz-z)^2)
    if distance>=rule.radius then return nil end
    return rule.gain*(1-math.max(0,distance-5)/(rule.radius-5))^2
end
local hit_controllers, hit_scheduled, hit_requests = {}, {}, {}
local hit_controller_count, hit_scheduled_count, hit_request_count = 0, 0, 0
local function observe(id, object, result)
    local last=recent[#recent]
    if last and last.id==id and last.object==object and last.result==result then
        last.count=last.count+1;last.frame=frame;return
    end
    recent[#recent+1]={id=id,object=object,result=result,frame=frame,count=1}
    if #recent>32 then table.remove(recent,1) end
end

local function normalize(s) return tostring(s):gsub("%s+", "") end
local function find_method(typename, name, types)
    local t = sdk.find_type_definition(typename)
    if not t then return nil end
    for _, m in ipairs(t:get_methods()) do
        if m:get_name() == name then
            local p, good = m:get_param_types(), true
            if #p == #types then
                for i = 1, #p do if normalize(p[i]:get_full_name()) ~= types[i] then good = false end end
                if good then return m end
            end
        end
    end
    return nil
end
local function integer(p) return sdk.to_int64(p) end
local function fresh()
    local age = host and type(host.timestamp)=="number" and os.time()-host.timestamp or 99
    return enabled and hooks_ready and host and host.version==2 and host.session==session
        and host.ready==true and age>=0 and age<=2
end
local function restore()
    suppressed = false
    if saved_device ~= nil then
        local ok = pcall(function()
            saved_device:call("set_ForceFeedbackEnable", saved_feedback)
            if saved_device:call("get_ForceFeedbackEnable") ~= saved_feedback then error("Native feedback restoration pending") end
        end)
        if ok then saved_device, saved_feedback = nil, nil; return true end
        return false
    end
    return true
end
local function fail(err)
    hooks_ready = false; error_text = tostring(err); restore()
    log.error("[DD2 DualSense] " .. error_text)
end
local function device()
    local manager = sdk.get_managed_singleton("app.UserPadManager")
    if not manager then return nil end
    local pads = manager:get_field("_PadList")
    if not pads then return nil end
    for i=0,pads:call("get_Count")-1 do
        local pad = pads:call("get_Item(System.Int32)",i)
        local d = pad and pad:get_field("Device")
        if d and tostring(d:get_type_definition():get_full_name()):find("VendorNativeDualSenseDevice",1,true) then return d end
    end
end
-- Native HD takes precedence only with a companion that negotiates USB handoff.
local native_hd_ids={[97291268]=true,[312821056]=true,[379474759]=true,[482704924]=true,[584489915]=true,[585869800]=true,[599479183]=true,[664812019]=true,[771805964]=true,[910631302]=true,[934905400]=true,[950605663]=true,[960962991]=true,[996203489]=true,[1079733981]=true,[1155592076]=true,[1163680941]=true,[1205658142]=true,[1529332519]=true,[1595888819]=true,[1600471081]=true,[1639119414]=true,[1762551309]=true,[1793781032]=true,[1993066807]=true,[2119695813]=true,[2146476090]=true,[2149414424]=true,[2186431642]=true,[2306059103]=true,[2338050813]=true,[2350094562]=true,[2510708406]=true,[2765774126]=true,[2793662280]=true,[2807786691]=true,[2818156234]=true,[2967536491]=true,[2975224248]=true,[3073055006]=true,[3175498239]=true,[3194912149]=true,[3246712375]=true,[3286509592]=true,[3287332762]=true,[3304840797]=true,[3335658832]=true,[3580780963]=true,[3595810265]=true,[3632479629]=true,[3641936838]=true,[3676160279]=true,[3757807074]=true,[3872226534]=true,[3923361188]=true,[3970730098]=true,[4019732542]=true,[4034308014]=true,[4131702128]=true,[4164574641]=true,[4240101446]=true,[4247356071]=true,[4287705698]=true,[4294684994]=true,[4294691792]=true}
local native_hd_playing={}
local native_hd_dirty=false
local function native_hd_active() return next(native_hd_playing)~=nil end
local function hybrid_usb()
    return fresh() and host.native_hd_priority==true
end
local function native_hd_priority()
    return hybrid_usb() and native_hd_active()
end
local function install_native_hd()
    local function hook(name,params,pre)
        local m=find_method("soundlib.SoundVibrationManager",name,params)
        if not m then error("Native HD lifecycle method missing: "..name) end
        sdk.hook(m,function(args)
            local ok,why=pcall(pre,args)
            if not ok then fail(why) end
        end,function(ret)return ret end)
    end
    hook("registerPlayingInfo",{"System.UInt32","System.UInt32"},function(args)
        local id,request=integer(args[3])&0xffffffff,integer(args[4])&0xffffffff
        if native_hd_ids[id] and request~=0 and request~=0xffffffff then
            native_hd_playing[request]=id;native_hd_dirty=true
        end
    end)
    hook("unregisterPlayingInfo",{"System.UInt32"},function(args)
        local request=integer(args[3])&0xffffffff
        if native_hd_playing[request] then native_hd_playing[request]=nil;native_hd_dirty=true end
    end)
    hook("stopHDVib",{"System.UInt32"},function(args)
        local id=integer(args[3])&0xffffffff
        for request,trigger in pairs(native_hd_playing) do
            if trigger==id then native_hd_playing[request]=nil;native_hd_dirty=true end
        end
    end)
    hook("stopAll",{},function()
        if next(native_hd_playing) then native_hd_playing={};native_hd_dirty=true end
    end)
end

local function update_suppression()
    if not fresh() or not player_go then hybrid_device=nil;restore(); return end
    if hybrid_usb() then
        -- USB HID sanitizing handles legacy rumble. Keep game PCM and its stop /
        -- reset callbacks alive throughout HD playback and the return to custom.
        if not restore() then return end
        hybrid_device=device()
        suppressed=hybrid_device~=nil -- Custom ownership; native HD stays enabled.
        return
    end
    hybrid_device=nil
    local d = device()
    if not d then restore(); return end
    if saved_device and tostring(d)~=tostring(saved_device) and not restore() then return end
    if not saved_device then saved_feedback=d:call("get_ForceFeedbackEnable");saved_device=d end
    if type(saved_feedback)~="boolean" then saved_device=nil;error("Unconfirmed feedback state") end
    -- Repeated setters may reset the engine's device output mode every frame.
    -- Like Onimusha, only write when the observed value actually changes.
    local feedback=d:call("get_ForceFeedbackEnable")
    if feedback~=false then
        d:call("set_ForceFeedbackEnable",false)
        feedback=d:call("get_ForceFeedbackEnable")
    end
    suppressed=feedback==false
    if not suppressed then error("Native suppression readback failed") end
end
local function in_player_hierarchy(go)
    if not go or not player_go then return false end
    if tostring(go)==tostring(player_go) then return true end
    local transform=go:call("get_Transform")
    for _=1,8 do
        if not transform then break end
        if tostring(transform:call("get_GameObject"))==tostring(player_go) then return true end
        transform=transform:call("get_Parent")
    end
    return false
end
local owner_accessors, owner_cache, owner_cache_count = {}, {}, 0
local function ownership_accessors(t)
    local name=t:get_full_name()
    if owner_accessors[name] then return owner_accessors[name] end
    local result={}
    local names={owner=true,ownercharacter=true,ownerchara=true,originalowner=true,caster=true,attacker=true,shooter=true}
    local current=t
    for _=1,8 do
        if not current then break end
        for _,f in ipairs(current:get_fields()) do
            local n=f:get_name()
            local plain=n:lower():gsub("k__backingfield",""):gsub("[^a-z]","")
            if names[plain] then result[#result+1]={field=n} end
        end
        for _,m in ipairs(current:get_methods()) do
            local n=m:get_name()
            if n:sub(1,4)=="get_" and names[n:sub(5):lower()] and #m:get_param_types()==0 then result[#result+1]={method=m} end
        end
        current=current:get_parent_type()
    end
    owner_accessors[name]=result;return result
end
local function remember_owner(key,owned)
    if owner_cache_count>=256 and not owner_cache[key] then owner_cache={};owner_cache_count=0 end
    if not owner_cache[key] then owner_cache_count=owner_cache_count+1 end
    owner_cache[key]={owned=owned,until_frame=frame+15}
    return owned
end
local function owns(go)
    -- Parent changes must be visible immediately, even with a cached owner result.
    if in_player_hierarchy(go) then return true end
    if not go or not player_go then return false end
    local key=tostring(go)
    local cached=owner_cache[key]
    if cached and frame<=cached.until_frame then return cached.owned end
    local visited,budget={},64
    local function visit(value,depth)
        if not value or depth>4 or budget<=0 then return false end
        local k=tostring(value);if visited[k] then return false end
        visited[k]=true;budget=budget-1
        if k==tostring(player_go) or k==tostring(player_character) then return true end
        local t=value:get_type_definition()
        local gameObject=t:get_method("get_GameObject")
        if gameObject and #gameObject:get_param_types()==0 then
            local g=gameObject:call(value)
            if g and in_player_hierarchy(g) then return true end
        end
        for _,a in ipairs(ownership_accessors(t)) do
            local ok,found=pcall(function()
                local ref
                if a.field then ref=value:get_field(a.field) else ref=a.method:call(value) end
                return ref and visit(ref,depth+1)
            end)
            if ok and found then return true end
        end
        return false
    end
    local ok,result=pcall(function()
        local current=go
        for _=1,8 do
            if not current then break end
            local components=current:call("get_Components")
            if components then for _,c in ipairs(components:get_elements()) do if visit(c,0) then return true end end end
            local transform=current:call("get_Transform")
            local parent=transform and transform:call("get_Parent")
            current=parent and parent:call("get_GameObject")
        end
        return false
    end)
    return remember_owner(key,ok and result==true)
end
local function update_player()
    -- Returning to the title destroys the component before ManualPlayer is
    -- cleared. get_GameObject can throw during that interval. This is a
    -- temporary absence, not a failed hook installation or IPC failure.
    local ok,player,go,id=pcall(function()
        local manager=sdk.get_managed_singleton("app.CharacterManager")
        local current=manager and manager:call("get_ManualPlayer")
        local object=current and current:call("get_GameObject")
        local sound=object and id_method and id_method:call(nil,object,0)
        return current,object,sound
    end)
    player_wait_reason=ok and "" or tostring(player)
    if not ok then player,go,id=nil,nil,nil end
    if not go or tostring(go)~=tostring(player_go) or tostring(player)~=tostring(player_character) then
        owned_ids={};switches={};states={};events={};owner_cache={};owner_cache_count=0
        nearby_positions={};nearby_count=0
        lifetimes={};lifetime_count=0
        hit_controllers={};hit_scheduled={};hit_requests={}
        hit_controller_count=0;hit_scheduled_count=0;hit_request_count=0
    end
    player_go=go;player_character=player
    if id and id~=0 then owned_ids[tostring(id)]=true end
end
local function copy(t)
    local r={}
    if t then for k,v in pairs(t) do r[k]=v end end
    return r
end
local function emit(id, object, request)
    if not enabled or not hooks_ready or not player_go then return end
    if not routes.events[tostring(id)] then observe(id,object,"uncatalogued");return end
    counts.matched=counts.matched+1
    -- Only the confirmed flesh-hit event may use damage-call provenance.
    -- Never promote an enemy emitter to general player ownership.
    local pending=request and hit_requests[request]
    if request then hit_requests[request]=nil end
    local outgoing=id==1701720996 and pending and frame<=pending.until_frame
    local level=1
    if not owned_ids[object] and not outgoing then
        level=nearby_level(id,object)
        if not level then counts.foreign=counts.foreign+1;observe(id,object,"foreign");return end
    end
    -- Drop sounds while stopped/disconnected instead of replaying them later.
    if not fresh() or not suppressed then observe(id,object,"inactive");return end
    observe(id,object,"queued")
    event_seq=event_seq+1
    local token=0
    if request and request>0 and request<0xffffffff and lifetime_count<128 then
        token=event_seq
        lifetimes[token]={token=token,request=request,playing=0,frame=frame,id=id,object=object}
        lifetime_count=lifetime_count+1
    end
    events[#events+1]={seq=event_seq,frame=frame,id=id,object=object,lifetime=token,level=level,switches=copy(switches[object]),states=copy(states)}
    if #events>128 then table.remove(events,1);counts.dropped=counts.dropped+1 end
    last_event=tostring(id)
end
local function update_lifetimes()
    -- Keep observing already-started requests across focus/device interruptions.
    -- Output is gated independently; ended requests must not resume afterwards.
    if not enabled or not player_go then local changed=lifetime_count>0;lifetimes={};lifetime_count=0;return changed end
    local changed=false
    for token,row in pairs(lifetimes) do
        local playing=playing_method:call(nil,row.request)
        -- Request IDs exist before the audio thread assigns a playing ID.
        -- Never interpret that initial zero as an immediate end, or loop an
        -- event which Wwise has not confirmed. Ended IDs disappear from the map.
        local ended=(playing==0 and row.playing~=0) or (row.playing==0 and frame-row.frame>120)
        if ended then
            lifetime_recent[#lifetime_recent+1]={token=token,id=row.id,request=row.request,playing=row.playing,frames=frame-row.frame}
            if #lifetime_recent>32 then table.remove(lifetime_recent,1) end
            lifetimes[token]=nil;lifetime_count=lifetime_count-1
            changed=true
        elseif playing~=0 and row.playing~=playing then row.playing=playing;changed=true end
    end
    return changed
end
local snapshot_rows={}
local function lifetime_snapshot()
    -- json.dump_file consumes these rows synchronously; no queued event owns them.
    -- Refresh every field and discard ended rows rather than caching lifetime state.
    local count=0
    for _,row in pairs(lifetimes) do
        count=count+1
        local result=snapshot_rows[count]
        if not result then result={};snapshot_rows[count]=result end
        result.token,result.confirmed,result.request,result.playing,result.id=row.token,row.playing~=0,row.request,row.playing,row.id
    end
    for i=#snapshot_rows,count+1,-1 do snapshot_rows[i]=nil end
    return snapshot_rows
end
local function hook_required(typename,name,types,pre,post)
    local m=find_method(typename,name,types)
    if not m then error("Missing method: "..typename.."."..name) end
    sdk.hook(m,pre,post or function(ret) return ret end)
    return m
end
local function safe_pre(fn)
    return function(args)
        local ok,result=pcall(fn,args)
        if not ok then fail(result);return sdk.PreHookResult.CALL_ORIGINAL end
        return result
    end
end
local function install_hit_provenance()
    local function typed(p,name)
        if not p or not sdk.is_managed_object(p) then return nil end
        local o=sdk.to_managed_object(p);local t=o:get_type_definition()
        for _=1,8 do
            if not t then return nil end
            if t:get_full_name()==name then return o end
            t=t:get_parent_type()
        end
    end
    local function receiver(args)
        for i=1,2 do
            local o=typed(args[i],"app.WwiseDamageController")
            if o then return o,i end
        end
    end
    local function guarded(fn)
        -- A destroyed object or unsupported optional path rejects the hit.
        -- Never interrupt the game's sound call or disable the primary bridge.
        return function(args)pcall(fn,args)end
    end
    local function unchanged(ret)return ret end
    local update=find_method("app.WwiseDamageController","updateInfo",{"app.HitController.DamageInfo"})
    local trigger=find_method("app.WwiseDamageController","updateTrigger",{})
    local clear=find_method("app.WwiseDamageController","clear",{})
    local posted=find_method("soundlib.SoundManager","postRequestInfo",{"soundlib.SoundManager.RequestInfo"})
    if not update or not trigger or not clear or not posted then return end
    sdk.hook(update,guarded(function(args)
        local o,slot=receiver(args)
        if not o then return end
        local key=tostring(o);hit_controllers[key]=nil
        local info=typed(args[slot+1],"app.HitController.DamageInfo")
        if not info then return end
        local attacker=info:call("get_AttackOwnerObject")
        local melee=info:get_field("IsShootAttack")==false and info:get_field("IsMagicAttack")==false
            and (info:get_field("IsSlashAttack")==true or info:get_field("IsBlowAttack")==true)
        local allowed=melee and player_go~=nil and attacker~=nil and in_player_hierarchy(attacker)
        if hit_controller_count>=256 then hit_controllers={};hit_controller_count=0 end
        hit_controllers[key]={allowed=allowed,until_frame=frame+2}
        hit_controller_count=hit_controller_count+1
    end),unchanged)
    sdk.hook(trigger,guarded(function(args)
        local o=receiver(args);if not o then return end
        local row=hit_controllers[tostring(o)]
        local container=o:call("get_CachedContainer")
        local id=o:get_field("TriggerId")
        if not container or type(id)~="number" or id<=0 then return end
        -- These fields identify the sound scheduled on a different engine job.
        -- Reject overlapping schedules instead of guessing which attack won.
        local key=tostring(container)..":"..tostring(id)
        local prior=hit_scheduled[key]
        local allowed=row~=nil and row.allowed and frame<=row.until_frame
            and not (prior~=nil and frame<=prior.until_frame)
        if hit_scheduled_count>=512 then hit_scheduled={};hit_scheduled_count=0 end
        hit_scheduled[key]={allowed=allowed,until_frame=frame+2}
        hit_scheduled_count=hit_scheduled_count+1
    end),unchanged)
    sdk.hook(clear,guarded(function(args)
        local o=receiver(args);if o then hit_controllers[tostring(o)]=nil end
    end),unchanged)
    sdk.hook(posted,function(args)
        local storage=thread.get_hook_storage();storage.dd2_outgoing_request=false
        guarded(function()
            local request=typed(args[1],"soundlib.SoundManager.RequestInfo") or typed(args[2],"soundlib.SoundManager.RequestInfo")
            if not request then return end
            local container=request:call("get_Container")
            local trigger_id=request:call("get_TriggerId")
            local key=container and tostring(container)..":"..tostring(trigger_id)
            local row=key and hit_scheduled[key]
            if key then hit_scheduled[key]=nil end
            storage.dd2_outgoing_request=row~=nil and row.allowed and frame<=row.until_frame
        end)()
    end,function(ret)
        pcall(function()
            local id=integer(ret)&0xffffffff
            if id==0 or id==0xffffffff then return end
            hit_requests[id]=nil
            if thread.get_hook_storage().dd2_outgoing_request then
                if hit_request_count>=256 then hit_requests={};hit_request_count=0 end
                hit_requests[id]={until_frame=frame+2};hit_request_count=hit_request_count+1
            end
        end)
        return ret
    end)
end

local function install()
    routes=json.load_file("dd2_dualsense_routes.json")
    if type(routes)~="table" or routes.version~=2 or type(routes.events)~="table" then error("Run Setup.cmd to install sound routes") end
    playing_method=find_method("via.simplewwise.Driver","getPlayingIdByRequestId",{"System.UInt32"})
    if not playing_method then error("Missing request lifetime lookup") end
    id_method=hook_required("via.simplewwise.Driver","getGameObjectId",{"via.GameObject","System.UInt32"},safe_pre(function(args)
        -- REFramework follows the wrapper JMP. The native target receives the
        -- GameObject in RCX (slot 1), and the UInt32 index in slot 2.
        local storage=thread.get_hook_storage()
        storage.dd2_owned=false
        storage.dd2_owner_known=false
        storage.dd2_nearby_x=nil
        if sdk.is_managed_object(args[1]) then
            -- A transient/destroyed emitter is simply not attributable. Never
            -- latch all hooks off because one component is being unloaded.
            local object=sdk.to_managed_object(args[1])
            local ok,owned=pcall(owns,object)
            storage.dd2_owned=ok and owned==true
            storage.dd2_owner_known=ok
            if ok and not owned and routes.nearby_events then
                local positioned,x,y,z=pcall(sound_position,object)
                if positioned then
                    storage.dd2_nearby_x,storage.dd2_nearby_y,storage.dd2_nearby_z=x,y,z
                    storage.dd2_nearby_frame=frame
                end
            end
        end
    end),function(ret)
        local ok,err=pcall(function()
            local storage=thread.get_hook_storage()
            local id=tostring(integer(ret))
            update_nearby(id,storage.dd2_nearby_x,storage.dd2_nearby_y,storage.dd2_nearby_z,storage.dd2_nearby_frame)
            if storage.dd2_owned then owned_ids[id]=true
            elseif storage.dd2_owner_known then owned_ids[id]=nil;if not nearby_positions[id] then switches[id]=nil end end
        end)
        if not ok then fail(err) end;return ret
    end)
    hook_required("via.simplewwise.SendRequest","postEvent",{"System.UInt64","System.UInt32","System.UInt32","via.simplewwise.CallbackType","System.Boolean","System.Int32","via.wwiselib.CurveInterpolationType"},safe_pre(function(args)
        counts.posted=counts.posted+1
        -- Live traces verify slot 4 is the request ID. The queued postEvent
        -- return is 0xffffffff, NOT a usable request or playing ID.
        emit(integer(args[3]) & 0xffffffff,tostring(integer(args[2])),integer(args[4]) & 0xffffffff)
    end))
    hook_required("via.simplewwise.SendRequest","unregisterGameObject",{"System.UInt64"},safe_pre(function(args)
        local id=tostring(integer(args[2]))
        owned_ids[id]=nil;switches[id]=nil
        forget_nearby(id)
    end))
    install_hit_provenance()
    local function suppress_void()
        if fresh() and suppressed and not hybrid_usb() then return sdk.PreHookResult.SKIP_ORIGINAL end
    end
    -- Current installed request table: only 89..95 carry an HD name.
    -- Keep their original dispatch so the game's HD timing/loop semantics survive.
    local hd_request_ids={[89]=true,[90]=true,[91]=true,[92]=true,[93]=true,[94]=true,[95]=true}
    local function block_regular()
        counts.regular_requests_blocked=(counts.regular_requests_blocked or 0)+1
        return sdk.PreHookResult.SKIP_ORIGINAL
    end
    hook_required("app.UserPadManager","requestVibration",{"app.UserPadDefine.RequestID","via.GameObject"},safe_pre(function(args)
        if fresh() and suppressed then
            if not hybrid_usb() or not hd_request_ids[integer(args[3])&0xffffffff] then return block_regular() end
        end
    end))
    hook_required("app.UserPadManager","requestVibrationHD",{"System.String","System.Boolean"},safe_pre(suppress_void))
    hook_required("app.UserPadManager","requestVibration",{"app.VibrationPresetRequestData","via.GameObject"},safe_pre(function(args)
        local skip=false
        if fresh() and suppressed then
            skip=not hybrid_usb()
            if not skip then
                local data=sdk.to_managed_object(args[3])
                local hd=data and data:get_field("RequestVibrationHD")
                skip=type(hd)~="string" or hd==""
            end
        end
        thread.get_hook_storage().dd2_skip=skip
        if skip then return block_regular() end
    end),function(ret)
        if thread.get_hook_storage().dd2_skip then return sdk.to_ptr(0) end
        return ret
    end)
    hook_required("via.hid.GamePadDevice","setMotorPower",{"via.hid.GamePadMotor","System.Single"},safe_pre(function(args)
        -- This wrapper also shifts the native receiver into RCX (slot 1).
        if not fresh() or not suppressed then return end
        local target=hybrid_usb() and hybrid_device or saved_device
        if not target or not sdk.is_managed_object(args[1]) or tostring(sdk.to_managed_object(args[1]))~=tostring(target) then return end
        counts.output_observed.setMotorPower=(counts.output_observed.setMotorPower or 0)+1
        if hybrid_usb() then
            -- Native wrapper: RCX=device, RDX=motor, XMM2=power (hook slot 3).
            -- Zero only legacy motor power; execute the setter so zero/stop state
            -- still reaches the game. HD PCM, resetMotors and feedback stay intact.
            args[3]=sdk.to_ptr(0)
            counts.output_blocked.setMotorPower=(counts.output_blocked.setMotorPower or 0)+1
        else
            return sdk.PreHookResult.SKIP_ORIGINAL
        end
    end))
    -- These output-only APIs are present in the installed game's type metadata.
    -- A false ForceFeedbackEnable alone does not prove that its native device
    -- stopped submitting reports. Guard the other managed output entry points
    -- while the same target device is leased; leave input and other pads alone.
    local function output_guard(typename,name,types,receiver_slot)
        local method=find_method(typename,name,types)
        if not method then return end
        sdk.hook(method,safe_pre(function(args)
            counts.output_observed[name]=(counts.output_observed[name] or 0)+1
            if not fresh() or not suppressed or hybrid_usb() or not saved_device then return end
            if sdk.is_managed_object(args[receiver_slot]) and tostring(sdk.to_managed_object(args[receiver_slot]))==tostring(saved_device) then
                counts.output_blocked[name]=(counts.output_blocked[name] or 0)+1
                return sdk.PreHookResult.SKIP_ORIGINAL
            end
        end),function(ret)return ret end)
    end
    output_guard("via.hid.GamePadDevice","resetMotors",{},1)
    output_guard("via.hid.GamePadDevice","set_ForceFeedbackEnable",{"System.Boolean"},2)
    -- WaveIndex resolves to a shared implementation (render/cloth/timeline calls
    -- observed live); do not hook it or interpret its traffic as device output.
    -- Optional, exact-signature hooks. Unsupported APIs leave bank defaults;
    -- they do not guess the material or claim an observed Switch value.
    local m=find_method("via.simplewwise.SendRequest","setSwitch",{"System.UInt64","System.UInt32","System.UInt32"})
    if m then sdk.hook(m,safe_pre(function(args)
        local object=tostring(integer(args[2]));if not owned_ids[object] and not nearby_positions[object] then return end
        switches[object]=switches[object] or {};switches[object][tostring(integer(args[3]) & 0xffffffff)]=integer(args[4]) & 0xffffffff
        counts.switch_updates=counts.switch_updates+1
    end),function(ret)return ret end) end
    counts.switch_hook=m~=nil
    install_native_hd()
    hooks_ready=true
end
local ok,err=pcall(install);if not ok then fail(err) end
re.on_frame(function()
    frame=frame+1
    local ok,err=pcall(function()
        if frame%15==1 then
            for id,p in pairs(nearby_positions) do
                if frame-p.frame>120 then
                    forget_nearby(id)
                    if not owned_ids[id] then switches[id]=nil end
                end
            end
            local loaded,value=pcall(json.load_file,CONTROL);host=loaded and value or nil
            if type(host)~="table" then host=nil end
            if #events>0 and host and host.session==session and type(host.ack)=="number" then
                local keep={};for _,e in ipairs(events) do if e.seq>host.ack then keep[#keep+1]=e end end;events=keep
            end
            update_player()
        end
        update_suppression()
        local lifetimes_changed=update_lifetimes()
        -- Keep unacknowledged events in each heartbeat for retry, but do not
        -- rewrite identical queued events / live loops on every rendered frame.
        -- Retry within the inbox's eight-frame acceptance window if a file
        -- read was missed; the slower heartbeat alone would be too late.
        local retry=#events>0 and frame-published_frame>=4
        if event_seq~=published_event_seq or retry or lifetimes_changed or suppressed~=published_suppressed or native_hd_dirty or frame%15==1 then
            snapshot=snapshot+1
            local result=json.dump_file(STATE,{version=2,session=session,seq=snapshot,frame=frame,events=events,
                enabled=enabled,hooks_ready=hooks_ready,player_ready=player_go~=nil and next(owned_ids)~=nil,
                native_hd_active=native_hd_priority(),hybrid_usb=hybrid_usb()==true,suppressed=suppressed,error=error_text,player_wait_reason=player_wait_reason,counts=counts,last_event=last_event,recent=recent,
                lifetimes=lifetime_snapshot(),lifetime_recent=lifetime_recent},-1)
            if result~=true then error("IPC write failed") end
            published_event_seq=event_seq;published_suppressed=suppressed
            published_frame=frame
            native_hd_dirty=false
        end
    end)
    if not ok then fail(err) end
end)
re.on_draw_ui(function()
    if imgui.tree_node("Dragon's Dogma 2 DualSense 0.1.0") then
        local changed,value=imgui.checkbox("Enable game-sound haptics",enabled)
        if changed then enabled=value;if not enabled then restore() end end
        imgui.text("Companion ready: "..tostring(fresh()==true))
        imgui.text("Native rumble suppressed: "..tostring(suppressed))
        imgui.text("Player sound ID found: "..tostring(next(owned_ids)~=nil))
        imgui.text("Last sound: "..last_event.." / observed: "..counts.posted.." / catalog matches: "..counts.matched)
        imgui.text("Switch hook: "..tostring(counts.switch_hook).." / updates: "..counts.switch_updates)
        if error_text~="" then imgui.text(error_text) end
        imgui.tree_pop()
    end
end)
re.on_script_reset(function()for _=1,3 do if restore() then break end end end)
log.info("[DD2 DualSense] Loaded game-sound bridge; companion handshake required.")
