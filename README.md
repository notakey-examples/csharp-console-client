# csharp-console-client

Console application that sends a verification request to 'demo.notakey.com' and awaits result

## Installing Notakey.SDK

The application relies on the NuGet package [Notakey.SDK](https://www.nuget.org/packages/Notakey.SDK/):

    Install-Package Notakey.SDK

## Using the SDK

To use it, you need:

- a valid Notakey server API endpoint
- a valid Notakey application AccessId value (on the same server)
- an onboarded user, which can approve authentication requests in this application

## Demo environment

For testing, we have provided a default (shared) environment:

- service domain demo.notakey.com
- application .NET SDK Demo (with AccessId 235879a9-a3f3-42b4-b13a-4836d0fd3bf8)
- user 'demo' with password 'demo' 

After onboarding this user in the Notakey Authenticator mobile application, and running this demo, you should
receive authentication requests.

### Expected output

![image](img.png)
