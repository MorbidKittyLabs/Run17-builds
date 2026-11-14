#include "fleet_readings_policy.h"
#include <cassert>
#include <limits>
#include <iostream>
using fleet_diagnostics::diagnostics::CargoSample;
using fleet_diagnostics::diagnostics::NonNegative;
int main() {
  const auto unknown = CargoSample::Read({}, {}, {});
  assert(!unknown.fill_fraction && !unknown.current && !unknown.protected_limit && !unknown.over_protected);
  const auto empty = CargoSample::Read(0, 0, 100);
  assert(empty.fill_fraction && *empty.fill_fraction == 0 && empty.over_protected && !*empty.over_protected);
  const auto protected_cargo = CargoSample::Read(0.75, 100, 100);
  assert(protected_cargo.over_protected && !*protected_cargo.over_protected);
  const auto opc = CargoSample::Read(1, 101, 100);
  assert(opc.over_protected && *opc.over_protected);
  assert(!CargoSample::Read(0.5, 1, {}).over_protected);
  assert(!CargoSample::Read(1.01, 1, 2).fill_fraction);
  assert(!CargoSample::Read(-0.1, -1, -2).current);
  assert(!CargoSample::Read(std::numeric_limits<double>::infinity(), 1, 2).fill_fraction);
  assert(!CargoSample::Read(0.5, std::numeric_limits<double>::quiet_NaN(), 2).over_protected);
  assert(!NonNegative(-1) && !NonNegative(std::numeric_limits<double>::infinity()));
  assert(NonNegative(0) && *NonNegative(0) == 0);
  std::cout << "PASS: unknown cargo stays unknown; zero is valid; exact protected boundary; invalid readings rejected\n";
}

