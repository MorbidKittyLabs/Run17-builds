#include "toml_editor.h"
#include "interior_zoom_policy.h"
#include "system_zoom_save_policy.h"
#include "config_save.h"
#include <cassert>
#include <iostream>
#include <tuple>
#include <vector>
int main(int argc,char** argv){
 assert(argc==2);
 for(bool far_source:{false,true}) {
  for(float ratio:{0.2f,0.3f,0.5f,0.8f})assert(interior_zoom_policy::Evaluate(0.2f,0.8f,ratio,2.0f,far_source).progress==0);
  const auto outer=interior_zoom_policy::Evaluate(0.2f,0.8f,far_source?-0.4f:1.4f,2.0f,far_source);
  assert(std::abs(outer.progress-1.0f)<0.00001f);
  assert((far_source?outer.maximum:outer.minimum)==(far_source?0.8f:0.2f));
 }
 config_edit::TomlEditor editor;
 const std::string text="# header\r\n[graphics] # section\r\nui_scale = 0.0 # keep\r\nunknown = 'preserve'\r\n";
 const auto edit=editor.Prepare(text,{"graphics","ui_scale",config_edit::Value{0.0},0.75});
 assert(edit.outcome==config_edit::Outcome::Prepared);
 assert(edit.text=="# header\r\n[graphics] # section\r\nui_scale = 0.75 # keep\r\nunknown = 'preserve'\r\n");
 const auto another=editor.Prepare(edit.text,{"control","queue_enabled",std::nullopt,false});
 assert(another.outcome==config_edit::Outcome::Prepared);
 assert(toml::parse(another.text)["control"]["queue_enabled"].value<bool>()==false);
 const auto path=std::filesystem::path(argv[1])/"v6-test.toml";
 ReplaceConfigText(path,text);
 assert(ReplaceConfigText(path,another.text,text));
 assert(!ReplaceConfigText(path,text,text)); // external changes cannot be overwritten using a stale baseline
 assert(ReadConfigText(path)==another.text);
 assert(editor.Prepare(text,{"graphics","ui_scale",config_edit::Value{1.0},0.8}).outcome==config_edit::Outcome::Conflict);
 assert(editor.Prepare("bad=[",{"graphics","ui_scale",std::nullopt,1.0}).outcome==config_edit::Outcome::InvalidDocument);
 // Two distinct camera saves must survive reopening and restart-style restoration.
 auto zoom_text=ReadConfigText(path);
 for(float distance:{1275.625f,842.125f}) {
  const auto before=zoom_text;
  const auto parsed=toml::parse(before);
  const auto old=parsed["dse_saved"]["system_zoom_distance"].value<double>();
  const auto edit_zoom=editor.Prepare(before,{"dse_saved","system_zoom_distance",old?std::optional<config_edit::Value>{*old}:std::nullopt,static_cast<double>(distance)});
  assert(edit_zoom.outcome==config_edit::Outcome::Prepared);
  assert(ReplaceConfigText(path,edit_zoom.text,before));
  zoom_text=ReadConfigText(path);assert(zoom_text==edit_zoom.text);
  const auto reloaded=toml::parse(zoom_text)["dse_saved"]["system_zoom_distance"].value<double>();
  assert(reloaded&&static_cast<float>(*reloaded)==distance);
  const auto restored=system_zoom_save::Restore(static_cast<float>(*reloaded),150.0f,6500.0f);
  assert(restored&&*restored==distance);
 }
 // One save preserves independent views and UI scales; changing one cannot erase the others.
 auto multi=ReadConfigText(path);
 for(const auto& [section,key,value]:std::vector<std::tuple<std::string,std::string,double>>{
   {"graphics","ui_scale",1.25},{"graphics","ui_scale_ship",2.1},{"graphics","ui_scale_viewer",0.75},
   {"dse_saved","galaxy_zoom_distance",3000.25},{"dse_saved","planet_zoom_distance",65.5},
   {"dse_saved","starbase_zoom_distance",135.25},{"dse_saved","interior_zoom_ratio",0.42},
   {"dse_saved","blend_zoom_385452890",0.33}}) {
   auto old=toml::parse(multi)[section][key].value<double>();
   auto edit=editor.Prepare(multi,{section,key,old?std::optional<config_edit::Value>{*old}:std::nullopt,value});
   assert(edit.outcome==config_edit::Outcome::Prepared);multi=edit.text;
 }
 auto before=ReadConfigText(path);assert(ReplaceConfigText(path,multi,before));
 auto document=toml::parse(ReadConfigText(path));
 assert(document["graphics"]["ui_scale"].value<double>()==1.25);
 assert(document["graphics"]["ui_scale_ship"].value<double>()==2.1);
 assert(document["graphics"]["ui_scale_viewer"].value<double>()==0.75);
 assert(document["dse_saved"]["system_zoom_distance"].value<double>()==842.125);
 assert(document["dse_saved"]["galaxy_zoom_distance"].value<double>()==3000.25);
 assert(document["dse_saved"]["planet_zoom_distance"].value<double>()==65.5);
 assert(document["dse_saved"]["starbase_zoom_distance"].value<double>()==135.25);
 assert(document["dse_saved"]["interior_zoom_ratio"].value<double>()==0.42);
 assert(document["dse_saved"]["blend_zoom_385452890"].value<double>()==0.33);
 assert(document["graphics"]["unknown"].value<std::string>()=="preserve");
 std::cout<<"PASS preserving multi-setting candidate, missing sections, checked replace and external-edit conflict\n";
}

