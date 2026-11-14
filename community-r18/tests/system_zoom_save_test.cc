#include "system_zoom_save_policy.h"
#include <cassert>
#include <limits>
#include <cmath>
int main(){
 using namespace system_zoom_save;
 Sample captured{1275.625f,50.0f,5000.0f,5000.0f,10000};
 assert(captured.Valid()&&captured.Fresh(10016)&&!captured.Fresh(11001)&&!captured.Fresh(9999));
 auto restored=Restore(captured.distance,150.0f,6500.0f);
 assert(restored&&*restored==captured.distance);
 const auto legacy_reconstructed=150.0f+(6500.0f-150.0f)*captured.Normalized()/5000.0f;
 assert(std::abs(legacy_reconstructed-captured.distance)>1.0f);
 assert(*Restore(captured.distance,2000,6500)==2000);
 assert(!Restore(std::numeric_limits<float>::quiet_NaN(),50,5000));
 assert(!Restore(1250,5000,50));
 Sample invalid=captured;invalid.distance=std::numeric_limits<float>::infinity();assert(!invalid.Valid());
}

