// OPLauncher.Hook.Native - Multi-client hooks for Asheron's Call
// Hooks:
// 1. Client::IsAlreadyRunning - Mutex bypass
// 2. CLBlockAllocator::OpenDataFile - File sharing

#include <Windows.h>

// Addresses of functions to hook in acclient.exe
#define CLIENT_ISALREADYRUNNING_ADDR 0x004122A0
#define CLBLOCKALLOCATOR_OPENDATAFILE_ADDR 0x00675920

// Original bytes and state for hooks
BYTE g_mutexOriginalBytes[16] = { 0 };
BYTE g_fileOriginalBytes[32] = { 0 };
BOOL g_hooksInstalled = FALSE;

// Address where we'll place our file opening hook code
void* g_fileHookTrampoline = NULL;

// Original function pointer for file opening
typedef DWORD(__thiscall* CLBlockAllocator_OpenDataFile_t)(void* thisPtr, void* pFileInfo, void* pFileName, void* pcPathToUse, DWORD openFlags, void* pTranInfo);
CLBlockAllocator_OpenDataFile_t g_originalOpenDataFile = NULL;

/**
 * Our replacement for CLBlockAllocator::OpenDataFile
 * Adds FILE_SHARE_READ flag to allow multiple clients to open the same files
 */
DWORD __fastcall OpenDataFile_Hook(void* thisPtr, void* unused_edx, void* pFileInfo, void* pFileName, void* pcPathToUse, DWORD openFlags, void* pTranInfo)
{
    // Add FILE_SHARE_READ flag (0x4) to the open flags
    // This allows multiple processes to read the same .dat files
    openFlags |= 0x4;

    // Call the original function with modified flags
    // Note: We need to restore __thiscall convention
    if (g_originalOpenDataFile)
    {
        return g_originalOpenDataFile(thisPtr, pFileInfo, pFileName, pcPathToUse, openFlags, pTranInfo);
    }
    return 0;
}

/**
 * Install the mutex bypass hook
 * Patches Client::IsAlreadyRunning to immediately return 0 (false)
 */
BOOL InstallMutexHook()
{
    DWORD oldProtect;
    void* hookAddr = (void*)CLIENT_ISALREADYRUNNING_ADDR;

    // Make the memory writable
    if (!VirtualProtect(hookAddr, 16, PAGE_EXECUTE_READWRITE, &oldProtect))
    {
        return FALSE;
    }

    // Save original bytes
    memcpy(g_mutexOriginalBytes, hookAddr, 16);

    // Write new assembly code:
    // xor eax, eax    ; Set return value to 0 (false - not already running)
    // ret             ; Return immediately
    BYTE patchBytes[] = {
        0x31, 0xC0,     // xor eax, eax
        0xC3            // ret
    };

    // Apply the patch
    memcpy(hookAddr, patchBytes, sizeof(patchBytes));

    // Restore original memory protection
    VirtualProtect(hookAddr, 16, oldProtect, &oldProtect);

    return TRUE;
}

/**
 * Install the file sharing hook
 * Hooks CLBlockAllocator::OpenDataFile to add FILE_SHARE_READ flag
 */
BOOL InstallFileHook()
{
    DWORD oldProtect;
    void* hookAddr = (void*)CLBLOCKALLOCATOR_OPENDATAFILE_ADDR;

    // Allocate memory for trampoline (original code + jump back)
    g_fileHookTrampoline = VirtualAlloc(NULL, 32, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    if (!g_fileHookTrampoline)
    {
        return FALSE;
    }

    // Make the target memory writable
    if (!VirtualProtect(hookAddr, 32, PAGE_EXECUTE_READWRITE, &oldProtect))
    {
        VirtualFree(g_fileHookTrampoline, 0, MEM_RELEASE);
        g_fileHookTrampoline = NULL;
        return FALSE;
    }

    // Save original bytes
    memcpy(g_fileOriginalBytes, hookAddr, 32);

    // Copy first 5 bytes of original function to trampoline
    memcpy(g_fileHookTrampoline, hookAddr, 5);

    // Add jump back to original function (after our hook)
    BYTE* trampolineJump = (BYTE*)g_fileHookTrampoline + 5;
    trampolineJump[0] = 0xE9; // JMP instruction
    DWORD jumpOffset = ((DWORD)hookAddr + 5) - ((DWORD)trampolineJump + 5);
    memcpy(trampolineJump + 1, &jumpOffset, 4);

    // Set the original function pointer to our trampoline
    g_originalOpenDataFile = (CLBlockAllocator_OpenDataFile_t)g_fileHookTrampoline;

    // Write jump to our hook at the original location
    BYTE jumpToHook[5];
    jumpToHook[0] = 0xE9; // JMP instruction
    DWORD hookOffset = ((DWORD)OpenDataFile_Hook) - ((DWORD)hookAddr + 5);
    memcpy(jumpToHook + 1, &hookOffset, 4);

    memcpy(hookAddr, jumpToHook, 5);

    // Restore original memory protection
    VirtualProtect(hookAddr, 32, oldProtect, &oldProtect);

    return TRUE;
}

/**
 * Install all hooks
 */
BOOL InstallAllHooks()
{
    if (!InstallMutexHook())
    {
        return FALSE;
    }

    if (!InstallFileHook())
    {
        return FALSE;
    }

    g_hooksInstalled = TRUE;
    return TRUE;
}

/**
 * Remove all hooks (restore original bytes)
 */
void RemoveAllHooks()
{
    if (!g_hooksInstalled)
        return;

    DWORD oldProtect;

    // Restore mutex hook
    void* mutexAddr = (void*)CLIENT_ISALREADYRUNNING_ADDR;
    if (VirtualProtect(mutexAddr, 16, PAGE_EXECUTE_READWRITE, &oldProtect))
    {
        memcpy(mutexAddr, g_mutexOriginalBytes, 16);
        VirtualProtect(mutexAddr, 16, oldProtect, &oldProtect);
    }

    // Restore file hook
    void* fileAddr = (void*)CLBLOCKALLOCATOR_OPENDATAFILE_ADDR;
    if (VirtualProtect(fileAddr, 32, PAGE_EXECUTE_READWRITE, &oldProtect))
    {
        memcpy(fileAddr, g_fileOriginalBytes, 32);
        VirtualProtect(fileAddr, 32, oldProtect, &oldProtect);
    }

    // Free trampoline
    if (g_fileHookTrampoline)
    {
        VirtualFree(g_fileHookTrampoline, 0, MEM_RELEASE);
        g_fileHookTrampoline = NULL;
    }

    g_hooksInstalled = FALSE;
}

/**
 * DLL Entry Point
 */
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        // Disable thread attach/detach notifications for performance
        DisableThreadLibraryCalls(hModule);
        // Hooks are installed by HookStartup() export, not here
        break;

    case DLL_PROCESS_DETACH:
        // Clean up hooks on unload
        RemoveAllHooks();
        break;
    }
    return TRUE;
}

/**
 * Exported function called by injector.dll to initialize the hooks
 * This is the entry point that injector.dll looks for
 */
extern "C" __declspec(dllexport) int HookStartup()
{
    // Install both hooks (mutex bypass + file sharing)
    if (InstallAllHooks())
    {
        return 1; // Success
    }
    return 0; // Failure
}
