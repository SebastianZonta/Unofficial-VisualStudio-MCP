# Speech — Debugging with Claude, through Visual Studio

What I say, slide by slide. Stage directions are in *[brackets]*.

---

## Slide 1 — Title

Hi everyone. For the next fifteen minutes I'm going to do something a little
unusual: I'm going to let an AI debug two running applications, live, using the
exact same Visual Studio debugger you use every day.

I'll keep the slides short, just enough context so the demo makes sense, and then
we go straight to the terminal.

*[next]*

## Slide 2 — Visual Studio

You all know Visual Studio, so this one is a formality. We write code in it, we
build in it, and we debug in it.

And that last part is the one I care about today. The debugger. Breakpoints, the
call stack, looking at live variables while the app is stopped. That's the most
powerful thing in the IDE, and it's been something only a human could drive.

Today we hand it to the AI.

*[next]*

## Slide 3 — Model Context Protocol

How do we do that? With MCP, the Model Context Protocol.

The easiest way to think about MCP is USB-C for AI tools. On one side you have an
AI client, in our case Claude Code. On the other side you have servers, and each
server exposes tools: one for files, one for GitHub, one for a database.

The nice part is that it's a standard. You write a server once, and any MCP client
can plug into it. So we wrote one for the Visual Studio debugger.

*[next]*

## Slide 4 — The bridge

This is the whole picture. Claude Code talks MCP to our server, VsDebugMcp.
VsDebugMcp drives the Visual Studio that's already open on my machine, and
Visual Studio debugs the app.

You'll hear Claude use three words during the demo, so let me give you them now.
A **VS instance** is just one Visual Studio window that's running. A **debug
session** is Claude's handle on one of those VS instances. And the **target
process** is the app we're debugging.

The server has twenty-one tools. Find the instances, attach, put breakpoints,
launch the app, read the stack, evaluate values, and detach at the end. The ones on
the slide are the ones you're going to see in a minute.

*[next]*

## Slide 5 — Why this approach

Why would you want this?

First, evidence instead of guesses. When an AI reads your code, it gives you a
hypothesis. When it stops the process and reads the actual values, it gives you a
fact. And that's what I'm going to ask Claude for: not "I think the bug is here",
but proof.

Second, it's your setup. There's nothing new to install in your app. It uses the
Visual Studio you already have open, with your solution loaded.

Third, it works across apps. One debug session per Visual Studio, so Claude can
follow a single request as it goes from one service to another.

And fourth, it's safe by default. Evaluating is read-only. If it wants to attach to
some random process or change a variable, it has to explicitly ask for consent.

*[next]*

## Slide 6 — Today's demo

So here's today's problem. Two ASP.NET Core apps, each one open in its own Visual
Studio. Orders is the frontend, and it calls the Pricing Engine over HTTP to get a
price.

Order one: one widget. The total is 108. That's correct.

Order two: ten widgets with a twenty percent coupon. The total is… 108 again. It
should be 756.

No exception, no error in the logs. Just a number that looks perfectly reasonable
and is wrong. That's the worst kind of bug.

And there are rules. Claude can't change code and can't restart anything. It's
allowed to read the code to find its way around, but it can only *conclude*
something from what the debugger shows it.

*[next]*

## Slide 7 — Let's debug

The instructions for Claude are already sitting in the demo folder. So all I have
to do is type one word.

*[switch to the terminal, type `start`, press Enter]*

---

## Live demo

*[Say each line when Claude reaches that step. Pause and let the screen talk in between.]*

**While it builds:**
First it makes sure both apps are built. Nothing interesting yet.

**When it lists the instances:**
Here it found both Visual Studio instances by itself, one for each solution. I
didn't tell it which windows to use.

**When it attaches:**
Now it opens two debug sessions. Session A for Orders, session B for Pricing.
They're completely isolated from each other.

**When it sets breakpoints:**
Notice it puts the breakpoints *before* anything is running. One checkpoint in each
app, so it can catch the request on both sides.

**When it launches the apps:**
Now it starts each app with the debugger attached from the very first line. You can
see the process IDs coming back.

**When it sends the orders:**
And now the same two orders I showed you on the slide. First the small one… then
the one with the coupon.

**When it stops and inspects:**
This is the part I like. It's stopped inside the running app right now, reading the
real values: what came in, what's stored, what goes out. It's not guessing.

*[point at the orderId on both sides]*
And look at the order ID. It's the same one in both processes, so we know both stops
belong to the same request.

**When it finds the cause:**
*[let Claude's explanation sit for a few seconds]*
Notice that it couldn't have closed this just by reading the code. The comments even
present that piece as an optimization. It needed the stopped process to prove it.

**When it cleans up:**
And it cleans up after itself. Breakpoints removed, sessions detached, Visual Studio
left exactly as it was.

**When the report appears:**
*[open `Demo/REPORT.md`]*
And this is the verdict. Every claim it makes quotes the debugger: where it stopped,
what state it was in, what the values were. If it can't quote it, it doesn't count.

*[back to the slides]*

---

## Slide 8 — By the numbers

A few numbers about the tool you just watched. Twenty-one tools, two hundred and
thirty-eight automated tests, around ten days, a hundred and twenty-nine commits.

And it was built with Claude Code, end to end: write the spec, split it into
issues, write the tests first, then run it for real against Visual Studio. So in a
way, the tool was built by the same kind of agent that just used it.

*[next]*

## Slide 9 — What's next

Where does this go next?

The first one is a live dashboard. Today you watched a terminal scroll. The idea is
to watch every tool call of a Claude conversation appear live, grouped by debug
session, so you can see exactly what the agent is doing and where.

The second one is flow-testing skills. What you just saw was a plan I wrote by hand:
which apps, where to stop, what to check. The skills write that for you. Claude
reads your code, writes the plan, basically acceptance criteria that the debugger
checks, runs it, and hands you a verdict. The first of those three skills is already
shipped.

And the third card is for you. If you had an agent that can drive your debugger,
what would you give it?

*[next]*

## Slide 10 — Questions

That's it from me. Questions? And feel free to ask in Spanish, no problem at all.

---

## If they ask…

**Does it change my code?**
No. Evaluating is read-only. Changing a variable needs explicit consent on every
single call, and today it never did it.

**Does it only work with Claude?**
No. MCP is a standard, so any MCP client can use the server.

**Does it only work with .NET?**
It drives the Visual Studio debugger, and launching apps is for .NET apps today.

**Can I point it at production?**
No. It's local, on your own machine, against your own Visual Studio, as your own user.

**Why not just read the logs?**
Because here the logs look fine. Nothing failed. The number is wrong, but it looks right.
