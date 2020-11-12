# ComNet
Several projects related to a COM dll written in dot net used to provide communications support to applications.

11/12/2020
==========
I am not a master dot Net developer.  These projects are designed to help me work out ideas and help me further develop my C# skills.

ComPack is a COM dll which provides the ability to communicate with another computer via file transfer, IRC, and TCP protocols.   Along with the files needed to install the DLL are client programs that allow you to choose a protocol and communicate with another client.   The client programs is written in VFP, with future versions to be written in C#, and Delphi.

The file protocol, like the TCP protocols, is designed to work on a local network.  The File protocol is useful for non-TCP/IP networks or
networks that have virtually all TCP communications shut down for security purposes.  The direct TCP and File protocols are typically used between two points on the network, though it is possible to communicate one-to-many with the file protocol.  There are two TCP protocols, one which is strictly one-to-one and the other that relies on a Chat Server in order to talk to other clients.

The Direct TCP protocol is helpful for setting up intranetwork communications between applications and offers a fairly simple encryption
which will be adequate for any low level security needs.

There is a lot that can be added to this project, especially for IRC support.  If you are so inclined, feel free to contact me.  The first time you contact me may take a day or three before I respond, so be patient.  If you put COMPACK in the subject, that will help speed things up.

Jon Walker
jlwalker.dev@gmail.com

