# 👀 WKVRCFT

A friendly fork of VRCFaceTracking — bridges face/eye-tracking hardware to VRChat over OSC, with a Photino+Vue host and hot-pluggable v2 module SDK.

## Credits

This project is a fork of [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking) by [benaclejames](https://github.com/benaclejames). The original tool is Apache-2.0–licensed; the same license carries through here. We've kept the upstream remote configured so improvements there can be pulled in over time.

Many thanks to the VRCFaceTracking community and contributors whose work made this fork possible.

## [Get started here!](https://docs.vrcft.io/docs/intro/getting-started)

[![Discord](https://discord.com/api/guilds/849300336128032789/widget.png)](https://discord.com/invite/vrcft)

## 🎥 Demo

[![](https://i.imgur.com/iQkw12C.jpg)](https://youtu.be/ZTVnh8aaf9U)

## 🛠 Avatar Setup

For this app to work, you'll need to be using an avatar with the correct parameters or an avatar config file with the correct mappings. The system is designed to control your avatar's eyes and lips via simple blend states but what the parameters control is completely up to you.

### [List of Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/)

## 👀 [Eye Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/#eye-tracking-parameters)

### [Eye Tracking Setup Guide](https://github.com/benaclejames/VRCFaceTracking/wiki/Eye-Tracking-Setup)

It's not required to use all of these parameters. In fact, you don't need to use any of them if you intend on using VRChat's built-in eye tracking system. Similar to the setup of parameters with Unity Animation Controllers, these are all case-sensitive and must be copied **EXACTLY** as shown into your Avatar's base parameters. A typical setup might look something like this:<br>
![](https://i.imgur.com/kfJD1Bl.png)

We strongly encourage you to [consult the docs](https://docs.vrcft.io) for a setup guide and more info as to what each parameter does

## :lips: [Lip and Face Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/#expression-tracking-parameters)

There are a large number of parameters you can use for lip and face tracking. 

### [Combined Lip Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/#addtional-simplified-tracking-parameters) - Combined parameters to group mutually exclusive face shapes.

## ⛓ External Modules

Use the module registry to download addons and add support for your hardware!
