# Kiss Machine

A completely automated, augmented reality photo booth!
To be used as an automated photo booth for weddings and parties. Uses Microsoft Kinect v2.

Each person recognized by the app is marked with a heart in the face. If two persons get close enough together, they are encouraged to give each other a kiss. A countdown starts, after which the app takes a photo. This is stored in your photo library and is also shown on the screen for several seconds.

The app interaction requires no haptic user interface: persons are recognized through the Microsoft Kinect, and the photos are automatically taken in the right moment. The interaction with your guests is done through text-to-speech and on-screen instructions.

This is a very lean project, as such the focus was on getting the functionality done as quickly as possible and not necessarily on creating re-usable code.

# Compatibility & Languages

This app has been tested under Windows 10 and requires a Microsoft Kinect v2 camera connected to the PC. Additionally, certain Sony cameras can be utilized to take higher quality photos. Even if using an additional Sony camera, you still need the Kinect in addition for the live stream and tracking users.

The app is localized to English and German and supports text-to-speech for both languages. Make sure you have the appropriate language and speech packs installed on your machine.

# Sony camera support

The Kinect camera is optimized for real-time image capturing with high frame rates. To take high quality photos in typical party environments with challenging lightning situations, a stand-alone photo camera (with a good lens or flash) can take much better photos. The Kiss Machine app supports interacting with Sony Cameras through the Sony Camera Remote API. [Supported camera models](https://developer.sony.com/develop/cameras/device-support/)

Take the photo with a Sony camera connected to the PC via WiFi Direct (instead of using the Kinect). The camera is completely remote controlled by the app. Tested with the Sony DSC-RX100M3.

Instructions:

1. Start the Remote Control mode on the camera, e.g., via "Smart Remote Control", "Turn Wi-Fi on" or "Control with Smartphone".
2. Connect the PC to the Wi-Fi of the camera.
3. Start this "Kiss Machine" app.

# Privacy Information

The app does not collect any specific usage data. The captured photos are only stored locally on your PC in the photo library. At events, please ensure you have the permission of your guests to take photos. 

# License

The complete source code of the app is available on GitHub under the open source GPL v3 license.
For interacting with the Sony Camera Remote API, the app incorporates part of the [kz-remote-api](https://github.com/kazyx/kz-remote-api) and the [kz-ssdp-discovery](https://github.com/kazyx/kz-ssdp-discovery) libraries developed by [kazyx](https://github.com/kazyx), released under MIT license.