#include "community_loading_policy.h"
#include <cassert>
int main(){
 const std::string old="# keep this comment\n[graphics]\nloader_enabled=false\nloader_transition=false\nloader_image='old-private-art.png'\nui_scale=0.75\n[patches]\nloadingscreenhooks=false\nloadingtiphooks=false\n[sync]\ntoken='fixture-keep-me'\n";
 const auto updated=community_loading::Restore(old);const auto parsed=toml::parse(updated);
 assert(parsed["graphics"]["loader_enabled"].value_or(false));
 assert(parsed["graphics"]["loader_image"].value_or("bad")=="");
 assert(parsed["patches"]["loadingscreenhooks"].value_or(false));
 assert(parsed["graphics"]["ui_scale"].value_or(0.0)==0.75);
 assert(parsed["sync"]["token"].value_or("")=="fixture-keep-me");
 assert(updated.starts_with("# keep this comment"));assert(community_loading::Restore(updated)==updated);
 auto later=updated;auto pos=later.find("loader_enabled=true");
 if(pos==std::string::npos)pos=later.find("loader_enabled = true");
 assert(pos!=std::string::npos);auto end=later.find('\n',pos);later.replace(pos,end-pos,"loader_enabled=false");
 assert(community_loading::Restore(later)==later);
}
