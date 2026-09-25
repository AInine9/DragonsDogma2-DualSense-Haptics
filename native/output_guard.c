// DD2 USB audio guard. Only a fresh companion lease enables output sanitizing.
// No input hooks, report blocking, game SDK offsets, or changes without a lease.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <hidsdi.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <wchar.h>
#include <stdatomic.h>
#include "MinHook.h"

typedef BOOL (WINAPI *WriteFn)(HANDLE,LPCVOID,DWORD,LPDWORD,LPOVERLAPPED);
typedef BOOLEAN (WINAPI *OutputFn)(HANDLE,PVOID,ULONG);
static WriteFn original_write;
static OutputFn original_output;
typedef BOOL (WINAPI *ResultFn)(HANDLE,LPOVERLAPPED,LPDWORD,BOOL);
typedef BOOL (WINAPI *ResultExFn)(HANDLE,LPOVERLAPPED,LPDWORD,DWORD,BOOL);
static ResultFn original_result;
static ResultExFn original_result_ex;
static _Atomic(uintptr_t) lease_pointer;
static HANDLE lease_mapping;
typedef struct {HANDLE handle;LPOVERLAPPED ov;void *bytes;uint64_t token;} Pending;
static Pending pending[256];
static uint64_t next_token,rewritten,pending_full;
static bool lease_valid(uint64_t deadline,uint64_t now) {return deadline>now && deadline-now<=2000;}
static bool leased(void) {
    uintptr_t pointer=atomic_load(&lease_pointer);
    return pointer && lease_valid(atomic_load((_Atomic(uint64_t)*)pointer),GetTickCount64());
}
static void sanitize(unsigned char *bytes) {
    // SDL's DualSense backend: flag0 bits0/1 select rumble/disable audio.
    // Linux hid-playstation: common.valid_flag2 bit2 selects improved rumble.
    bytes[1]&=(unsigned char)~3;bytes[39]&=(unsigned char)~4;
    bytes[3]=0;bytes[4]=0;
}
static SRWLOCK lock=SRWLOCK_INIT;
typedef struct { unsigned api,length; unsigned char bytes[64]; uint64_t calls,accepted; uintptr_t caller; } Sample;
static Sample samples[64];
static unsigned used;
static uint64_t dropped;
static wchar_t destination[MAX_PATH],temporary[MAX_PATH];
static LONG initialized;
static _Thread_local bool inside;

static uint64_t retain(HANDLE handle,LPOVERLAPPED ov,const unsigned char *bytes,unsigned n,void **buffer) {
    void *copy=HeapAlloc(GetProcessHeap(),0,n);if(!copy)return 0;memcpy(copy,bytes,n);
    AcquireSRWLockExclusive(&lock);
    unsigned i;for(i=0;i<256;i++)if(!pending[i].bytes)break;
    if(i==256){pending_full++;ReleaseSRWLockExclusive(&lock);HeapFree(GetProcessHeap(),0,copy);return 0;}
    uint64_t token=++next_token;pending[i]=(Pending){handle,ov,copy,token};
    ReleaseSRWLockExclusive(&lock);*buffer=copy;return token;
}
static void release_pending(HANDLE handle,LPOVERLAPPED ov,uint64_t token) {
    void *buffer=NULL;AcquireSRWLockExclusive(&lock);
    for(unsigned i=0;i<256;i++)if(pending[i].bytes && pending[i].handle==handle && pending[i].ov==ov && (!token||pending[i].token==token)) {
        buffer=pending[i].bytes;pending[i]=(Pending){0};break;
    }
    ReleaseSRWLockExclusive(&lock);if(buffer)HeapFree(GetProcessHeap(),0,buffer);
}
static BOOL WINAPI observe_result(HANDLE h,LPOVERLAPPED ov,LPDWORD size,BOOL wait) {
    BOOL result=original_result(h,ov,size,wait);DWORD error=GetLastError();
    if(result||error==ERROR_OPERATION_ABORTED)release_pending(h,ov,0);
    SetLastError(error);return result;
}
static BOOL WINAPI observe_result_ex(HANDLE h,LPOVERLAPPED ov,LPDWORD size,DWORD timeout,BOOL alertable) {
    BOOL result=original_result_ex(h,ov,size,timeout,alertable);DWORD error=GetLastError();
    if(result||error==ERROR_OPERATION_ABORTED)release_pending(h,ov,0);
    SetLastError(error);return result;
}

static bool copy_report(const void *buffer,DWORD length,unsigned char copy[64]) {
    SIZE_T read=0;
    return buffer && length>=48 && length<=64 &&
        ReadProcessMemory(GetCurrentProcess(),buffer,copy,length,&read) && read==length && copy[0]==2;
}
#ifdef OBSERVER_TEST
static bool force_test_handle;
#endif
static bool sony_handle(HANDLE handle) {
#ifdef OBSERVER_TEST
    if(force_test_handle)return true;
#endif
    HIDD_ATTRIBUTES attr={0};attr.Size=sizeof(attr);
    return HidD_GetAttributes(handle,&attr) && attr.VendorID==0x054c &&
        (attr.ProductID==0x0ce6 || attr.ProductID==0x0df2);
}
static void record(unsigned api,const unsigned char *bytes,unsigned length,uintptr_t caller,bool accepted) {
    AcquireSRWLockExclusive(&lock);
    unsigned i;
    for(i=0;i<used;i++)if(samples[i].api==api && samples[i].length==length &&
        samples[i].caller==caller && !memcmp(samples[i].bytes,bytes,length))break;
    if(i==used) {
        if(used==64){dropped++;ReleaseSRWLockExclusive(&lock);return;}
        samples[i].api=api;samples[i].length=length;samples[i].caller=caller;
        memcpy(samples[i].bytes,bytes,length);used++;
    }
    samples[i].calls++;if(accepted)samples[i].accepted++;
    ReleaseSRWLockExclusive(&lock);
}
static BOOL WINAPI observe_write(HANDLE h,LPCVOID b,DWORD n,LPDWORD written,LPOVERLAPPED ov) {
    if(inside)return original_write(h,b,n,written,ov);
    DWORD before=GetLastError();inside=true;
    unsigned char copy[64];bool match=copy_report(b,n,copy) && sony_handle(h);
    unsigned char changed[64];const void *send=b;uint64_t token=0;
    if(match&&leased()) {
        memcpy(changed,copy,n);sanitize(changed);
        if(ov) {void *held=NULL;token=retain(h,ov,changed,n,&held);if(token)send=held;}
        else send=changed;
        if(send!=b){AcquireSRWLockExclusive(&lock);rewritten++;ReleaseSRWLockExclusive(&lock);}
    }
    SetLastError(before);
    BOOL result=original_write(h,send,n,written,ov);DWORD after=GetLastError();
    if(token&&(result||after!=ERROR_IO_PENDING))release_pending(h,ov,token);
    if(match)record(1,copy,n,(uintptr_t)__builtin_return_address(0),result || after==ERROR_IO_PENDING);
    inside=false;SetLastError(after);return result;
}
static BOOLEAN WINAPI observe_output(HANDLE h,PVOID b,ULONG n) {
    if(inside)return original_output(h,b,n);
    DWORD before=GetLastError();inside=true;
    unsigned char copy[64];bool match=copy_report(b,n,copy) && sony_handle(h);
    unsigned char changed[64];void *send=b;
    if(match&&leased()) {
        memcpy(changed,copy,n);sanitize(changed);send=changed;
        AcquireSRWLockExclusive(&lock);rewritten++;ReleaseSRWLockExclusive(&lock);
    }
    SetLastError(before);
    BOOLEAN result=original_output(h,send,n);DWORD after=GetLastError();
    if(match)record(2,copy,n,(uintptr_t)__builtin_return_address(0),result!=0);
    inside=false;SetLastError(after);return result;
}
static void snapshot(void) {
    Sample rows[64];unsigned count,inflight=0;uint64_t lost,changed,full;
    AcquireSRWLockShared(&lock);count=used;lost=dropped;changed=rewritten;full=pending_full;
    for(unsigned i=0;i<256;i++)if(pending[i].bytes)inflight++;
    memcpy(rows,samples,sizeof(rows));ReleaseSRWLockShared(&lock);
    FILE *f=_wfopen(temporary,L"wb");if(!f)return;
    fprintf(f,"{\"version\":2,\"pid\":%lu,\"tick_ms\":%llu,\"lease_active\":%s,\"rewritten\":%llu,\"pending\":%u,\"pending_full\":%llu,\"dropped_patterns\":%llu,\"reports\":[",GetCurrentProcessId(),GetTickCount64(),leased()?"true":"false",changed,inflight,full,lost);
    for(unsigned i=0;i<count;i++) {
        MEMORY_BASIC_INFORMATION mbi={0};char module[MAX_PATH]={0};uintptr_t offset=0;
        if(VirtualQuery((void*)rows[i].caller,&mbi,sizeof(mbi))) {
            GetModuleFileNameA((HMODULE)mbi.AllocationBase,module,MAX_PATH);
            offset=rows[i].caller-(uintptr_t)mbi.AllocationBase;
        }
        char *name=strrchr(module,'\\');name=name?name+1:module;
        // Module basenames only; restrict characters before writing JSON.
        for(char *p=name;*p;p++)if(!((*p>='a'&&*p<='z')||(*p>='A'&&*p<='Z')||(*p>='0'&&*p<='9')||*p=='.'||*p=='_'||*p=='-'))*p='_';
        fprintf(f,"%s{\"api\":\"%s\",\"caller_module\":\"%s\",\"caller_offset\":\"%llx\",\"length\":%u,\"calls\":%llu,\"accepted_or_pending\":%llu,\"hex\":\"",i?",":"",rows[i].api==1?"WriteFile":"HidD_SetOutputReport",name,(unsigned long long)offset,rows[i].length,rows[i].calls,rows[i].accepted);
        for(unsigned j=0;j<rows[i].length;j++)fprintf(f,"%02x",rows[i].bytes[j]);
        fprintf(f,"\"}");
    }
    fprintf(f,"]}\n");fclose(f);
    MoveFileExW(temporary,destination,MOVEFILE_REPLACE_EXISTING);
}
static DWORD WINAPI writer(void *unused) {
    (void)unused;
    for(;;){
        if(!atomic_load(&lease_pointer)) {
            lease_mapping=OpenFileMappingW(FILE_MAP_READ,FALSE,L"Local\\DD2DualSenseNativeAudioLeaseV1");
            if(lease_mapping) {
                void *view=MapViewOfFile(lease_mapping,FILE_MAP_READ,0,0,8);
                if(view)atomic_store(&lease_pointer,(uintptr_t)view);
                else {CloseHandle(lease_mapping);lease_mapping=NULL;}
            }
        }
        snapshot();Sleep(200);
    }
    return 0;
}
__declspec(dllexport) bool reframework_plugin_initialize(const void *unused) {
    (void)unused;
    if(InterlockedCompareExchange(&initialized,1,0))return true;
    wchar_t exe[MAX_PATH];DWORD n=GetModuleFileNameW(NULL,exe,MAX_PATH);
    if(!n||n>=MAX_PATH)return false;
    wchar_t *last=wcsrchr(exe,L'\\');if(!last||_wcsicmp(last+1,L"DD2.exe"))return false;
    *last=0;
    if(swprintf(destination,MAX_PATH,L"%ls\\reframework\\data\\dd2_output_reports.json",exe)<0)return false;
    if(swprintf(temporary,MAX_PATH,L"%ls.tmp",destination)<0)return false;
    HMODULE kernel=GetModuleHandleW(L"kernel32.dll"),hid=LoadLibraryW(L"hid.dll");
    if(!kernel||!hid)return false;
    void *targets[4]={(void*)GetProcAddress(kernel,"WriteFile"),(void*)GetProcAddress(hid,"HidD_SetOutputReport"),
        (void*)GetProcAddress(kernel,"GetOverlappedResult"),(void*)GetProcAddress(kernel,"GetOverlappedResultEx")};
    void *hooks[4]={observe_write,observe_output,observe_result,observe_result_ex};
    void **originals[4]={(void**)&original_write,(void**)&original_output,(void**)&original_result,(void**)&original_result_ex};
    if(MH_Initialize()!=MH_OK)return false;
    unsigned created=0;
    for(;created<4;created++) {
        if(!targets[created]||MH_CreateHook(targets[created],hooks[created],originals[created])!=MH_OK)goto failed;
        if(MH_QueueEnableHook(targets[created])!=MH_OK){created++;goto failed;}
    }
    if(MH_ApplyQueued()!=MH_OK)goto failed;
    // Hooks and writer must remain mapped until process exit, including shutdown.
    HMODULE self=NULL;
    if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,(LPCWSTR)&reframework_plugin_initialize,&self)) {
        goto failed;
    }
    HANDLE thread=CreateThread(NULL,0,writer,NULL,0,NULL);
    if(!thread)goto failed;
    CloseHandle(thread);return true;
failed:
    for(unsigned i=0;i<created;i++){MH_DisableHook(targets[i]);MH_RemoveHook(targets[i]);}
    return false;
}
