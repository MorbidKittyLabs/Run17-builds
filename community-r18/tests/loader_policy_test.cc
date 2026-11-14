#include "loader_policy.h"
#include <cassert>
#include <iostream>
#include <string>

int main()
{
  // Demonstrate the regression with the original mixed-case launch spelling.
  assert(!std::wstring(L"Prime.exe").starts_with(L"prime"));
  for (unsigned mask = 0; mask < 32; ++mask) {
    std::wstring name = L"prime.exe";
    for (unsigned i = 0; i < 5; ++i) if (mask & (1u << i)) name[i] -= L'a' - L'A';
    assert(LoaderPolicy::IsPrimeExecutableName(name));
  }
  assert(LoaderPolicy::IsPrimeExecutableName(L"Prime-copy.exe"));
  for (auto name : {L"", L"prim", L"notprime.exe", L"wscript.exe", L"pr1me.exe"})
    assert(!LoaderPolicy::IsPrimeExecutableName(name));
  std::cout << "All 32 launch-name case combinations accepted; unrelated names rejected.\n";
}

