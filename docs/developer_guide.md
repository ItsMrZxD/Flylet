# Developer Guide

This guide provides instructions on how to build the project and test your changes.

* [Prerequisites](#prerequisites)
* [Building the project](#building-the-project)
* [Running](#running)

## Prerequisites

### System Requirements

- Windows 10 20H2 or recent insider builds (recommended).
- At least 4 GB of RAM and x64 based CPU (recommended).
- (optional) Any git client such as [Git for Windows](https://git-scm.com/download/win) or [GitHub Desktop](https://desktop.github.com/).
   - **GitHub Extension for Visual Studio** is enough for this project and is recommended for beginners.

### Visual Studio

Install latest VS2019 (stable or preview) from here: http://visualstudio.com/downloads

Include the following workloads:
- .NET desktop development
- Desktop development with C++
- MSIX Packaging Tools
- GitHub Extension for Visual Studio (recommended)

## Clone the repository

You can clone the repository using any git or GitHub Client of your choice.
You can even use Visual Studio's **Clone a repository** or GitHub's **Download Code as ZIP** feature.

After you have cloned the repository locally, open the **Flylet.sln** file on Visual Studio 2019.

## Building the project

Before building the project, please make sure setup your development environment properly and all dependency **Nuget Packages** are restored properly.

> Make sure you set the **Active Solution Configuration** to **Debug** and **Active Solution Configuration Platform** to **x64** in Visual Studio's **Configuration Manager**.

The **Flylet** project is the main one that contains all application source code.

But what you really need to do is to build the **Flylet.Package** project. It's the project that packages the app and run it properly.

Before building the **Flylet.Package** project, please make sure to do these things:

- Open the **Package.appxmanifest** on the **Flylet.Package**.
- Under the **Application** tab, please change the **Display Name** from **Flylet (Preview)** to anything else such as **Flylet (Dev)**
    - This helps you to distinguish between the actual installation of **Flylet** and this test build.
- Now go to the **Packaging** tab.
- **(!!!Important!!!)** Please change the **Package name** from **32669SamG.Flylet** to something like **(YourName).Flylet** and the **Package display name** from **Flylet (Preview)** to **Flylet (Dev)**
    - This will prevent the actual installation of **Flylet** from being overwritten by this test build.
- Now click on the **Choose Certificate** button next to the **Publisher** textbox
    - On the **Choose a Certificate** dialog, you can either click on **Select from store** and click **Ok** on the next dialog. (or)
    - You can click on **Create**, type in your name on the next dialog and click **Ok**.

That's it! Now you can actually build the app!

Choose the **Flylet.Package** project as the **Startup Project** and select **Build > Build Flylet.Package** or press <kbd>Ctrl</kbd> + <kbd>B</kbd>.

**Important Note:** When committing changes to the repo, please make sure you don't include the changes to the **Flylet.Package.wapproj** and the **Package.appxmanifest** files.

## Running

Never run the **Flylet** project. It will run a dummy app with no support for app-data.
Please make sure run the **Flylet.Package** project! If you have Flylet pre-installed on your computer, please make sure to close it before running this one.
Now to run the app, make sure **Flylet.Package** is the startup project and click on the **▶ Local Machine** button or **Debug > Start Debugging** or press <kbd>F5</kbd> to run the app.
