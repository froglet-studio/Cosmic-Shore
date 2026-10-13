# Switches a Unity-exported Xcode project to manual App Store signing, for the TestFlight
# workflow (.github/workflows/ios-testflight.yml; Docs/IOS_BUILD.md section 2, Path C).
#
#   ruby set_manual_signing.rb <Unity-iPhone.xcodeproj> <team id> <profile uuid> <profile name> <identity>
#
# Why a script and not xcodebuild arguments: a build setting passed on the command line applies to
# EVERY target, and UnityFramework is a framework. A provisioning profile on it fails the archive
# with "UnityFramework does not support provisioning profiles". So the profile goes on the app
# target only. Every target gets the team, manual style and the distribution identity; frameworks
# and static libraries need no profile.
require 'xcodeproj'

abort 'usage: set_manual_signing.rb <xcodeproj> <team> <profile uuid> <profile name> <identity>' if ARGV.length != 5
path, team, profile_uuid, profile_name, identity = ARGV

project = Xcodeproj::Project.open(path)
app = project.targets.find { |t| t.name == 'Unity-iPhone' }
abort "no Unity-iPhone target in #{path}" unless app

attributes = project.root_object.attributes['TargetAttributes'] ||= {}

project.targets.each do |target|
  is_app = target.equal?(app)
  target.build_configurations.each do |config|
    settings = config.build_settings
    settings['DEVELOPMENT_TEAM'] = team
    settings['CODE_SIGN_STYLE'] = 'Manual'
    settings['CODE_SIGN_IDENTITY'] = identity
    settings['CODE_SIGN_IDENTITY[sdk=iphoneos*]'] = identity
    settings['PROVISIONING_PROFILE_SPECIFIER'] = is_app ? profile_name : ''
    settings['PROVISIONING_PROFILE'] = is_app ? profile_uuid : ''
  end
  # The legacy per-target attribute Xcode still reads; left at Automatic it reports the target as
  # "automatically signed" and refuses the manual profile.
  target_attributes = attributes[target.uuid] ||= {}
  target_attributes['ProvisioningStyle'] = 'Manual'
  target_attributes['DevelopmentTeam'] = team
  puts "#{target.name}: manual, #{identity}#{is_app ? ", profile #{profile_name}" : ''}"
end

project.save
