const pptxgen = require("pptxgenjs");
const React = require("react");
const ReactDOMServer = require("react-dom/server");
const sharp = require("sharp");
const fa = require("react-icons/fa");

const OUT = process.argv[2];

const C = {
  bg: "17111F",
  surface: "251B33",
  surface2: "31244A",
  purple: "8B5CF6",
  purpleDeep: "68217A",
  amber: "F5A524",
  green: "34D399",
  red: "F87171",
  text: "F5F1FB",
  muted: "B4A9C8",
};
const H = "Arial";
const B = "Calibri";

async function icon(Comp, color, size = 256) {
  const svg = ReactDOMServer.renderToStaticMarkup(
    React.createElement(Comp, { color: "#" + color, size: String(size) })
  );
  const buf = await sharp(Buffer.from(svg)).png().toBuffer();
  return "image/png;base64," + buf.toString("base64");
}

// Motif: icon inside a colored circle.
async function badge(slide, Comp, x, y, d, fill, fg = C.text) {
  slide.addShape("ellipse", { x, y, w: d, h: d, fill: { color: fill }, line: { color: fill } });
  const pad = d * 0.25;
  slide.addImage({ data: await icon(Comp, fg), x: x + pad, y: y + pad, w: d - 2 * pad, h: d - 2 * pad });
}

function title(slide, text, sub) {
  slide.addText(text, { x: 0.5, y: 0.35, w: 9, h: 0.7, fontFace: H, fontSize: 36, bold: true, color: C.text, margin: 0 });
  if (sub)
    slide.addText(sub, { x: 0.5, y: 1.05, w: 9, h: 0.4, fontFace: B, fontSize: 18, italic: true, color: C.amber, margin: 0 });
}

function card(slide, x, y, w, h, fill = C.surface) {
  slide.addShape("roundRect", { x, y, w, h, fill: { color: fill }, line: { color: fill }, rectRadius: 0.12 });
}

function arrow(slide, x1, y, x2, color = C.muted) {
  slide.addShape("line", { x: x1, y, w: x2 - x1, h: 0, line: { color, width: 2.5, endArrowType: "triangle" } });
}

(async () => {
  const pres = new pptxgen();
  pres.layout = "LAYOUT_16x9";
  pres.title = "Debugging with Claude through Visual Studio";

  const base = () => {
    const s = pres.addSlide();
    s.background = { color: C.bg };
    return s;
  };

  // 1. Title
  {
    const s = base();
    await badge(s, fa.FaRobot, 0.5, 0.9, 0.9, C.amber, C.bg);
    s.addShape("line", { x: 1.55, y: 1.35, w: 0.7, h: 0, line: { color: C.muted, width: 2.5, endArrowType: "triangle" } });
    await badge(s, fa.FaBug, 2.4, 0.9, 0.9, C.purple);
    s.addText("Debugging with Claude,\nthrough Visual Studio", {
      x: 0.5, y: 2.1, w: 9, h: 1.6, fontFace: H, fontSize: 44, bold: true, color: C.text, margin: 0, valign: "top",
    });
    s.addText("An MCP server for the Visual Studio 2026 debugger", {
      x: 0.5, y: 3.75, w: 9, h: 0.45, fontFace: B, fontSize: 20, italic: true, color: C.amber, margin: 0,
    });
    s.addText("Sebastián Zonta", { x: 0.5, y: 4.7, w: 5, h: 0.4, fontFace: B, fontSize: 16, color: C.muted, margin: 0 });
    s.addNotes("Welcome. Three minutes of context, then a live demo where Claude debugs two running apps through Visual Studio.");
  }

  // 2. Visual Studio
  {
    const s = base();
    title(s, "Visual Studio", "The IDE — and its debugger is the star");
    const rows = [
      [fa.FaCode, "Write", "Editor, IntelliSense, refactoring", C.surface2],
      [fa.FaHammer, "Build", "Solutions, projects, .NET", C.surface2],
      [fa.FaBug, "Debug", "Breakpoints · call stack · live variables", C.purple],
    ];
    let y = 1.75;
    for (const [I, head, body, fill] of rows) {
      await badge(s, I, 0.5, y, 0.75, fill);
      s.addText(head, { x: 1.45, y: y + 0.02, w: 4.5, h: 0.38, fontFace: H, fontSize: 20, bold: true, color: C.text, margin: 0 });
      s.addText(body, { x: 1.45, y: y + 0.4, w: 4.5, h: 0.32, fontFace: B, fontSize: 15, color: C.muted, margin: 0 });
      y += 1.05;
    }
    card(s, 6.4, 1.75, 3.1, 2.85);
    s.addText("VS logo\n(optional)", {
      x: 6.4, y: 1.75, w: 3.1, h: 2.85, fontFace: B, fontSize: 14, italic: true, color: C.muted, align: "center", valign: "middle",
    });
    s.addNotes("Formality slide. Everyone here knows VS. The one thing to remember: the debugger. Breakpoints, stack, variables — that's what we'll hand to the AI.");
  }

  // 3. MCP
  {
    const s = base();
    title(s, "Model Context Protocol", "“USB-C for AI tools”");
    // client
    card(s, 0.5, 2.3, 2.2, 1.5, C.amber);
    s.addText([
      { text: "AI client", options: { fontSize: 18, bold: true, breakLine: true } },
      { text: "Claude Code", options: { fontSize: 14 } },
    ], { x: 0.5, y: 2.3, w: 2.2, h: 1.5, fontFace: H, color: C.bg, align: "center", valign: "middle" });
    // protocol hub
    await badge(s, fa.FaPlug, 3.45, 2.6, 0.9, C.purple);
    s.addText("MCP", { x: 3.2, y: 3.55, w: 1.4, h: 0.35, fontFace: H, fontSize: 14, bold: true, color: C.text, align: "center", margin: 0 });
    arrow(s, 2.8, 3.05, 3.38);
    // servers
    const servers = [
      [fa.FaFolderOpen, "Files"],
      [fa.FaGithub, "GitHub"],
      [fa.FaDatabase, "Databases"],
      [fa.FaBug, "Visual Studio debugger"],
    ];
    let y = 1.65;
    for (let i = 0; i < servers.length; i++) {
      const [I, label] = servers[i];
      const hi = i === servers.length - 1;
      const ty = y + 0.3, up = ty < 3.05;
      s.addShape("line", {
        x: 4.45, y: Math.min(ty, 3.05), w: 1.1, h: Math.max(Math.abs(ty - 3.05), 0.01), flipV: up,
        line: { color: hi ? C.amber : C.muted, width: hi ? 2.5 : 1.25 },
      });
      card(s, 5.6, y, 3.9, 0.6, hi ? C.purple : C.surface);
      s.addImage({ data: await icon(I, C.text), x: 5.8, y: y + 0.14, w: 0.32, h: 0.32 });
      s.addText(label, { x: 6.3, y, w: 3.1, h: 0.6, fontFace: B, fontSize: 16, bold: hi, color: C.text, valign: "middle", margin: 0 });
      y += 0.8;
    }
    s.addText("One standard plug: any client, any tool.", {
      x: 0.5, y: 4.85, w: 9, h: 0.35, fontFace: B, fontSize: 14, italic: true, color: C.muted, margin: 0,
    });
    s.addNotes("MCP: an open standard so an AI client can call tools exposed by servers. Write a server once, every MCP client can use it. Ours exposes the VS debugger.");
  }

  // 4. The bridge
  {
    const s = base();
    title(s, "The bridge", "VsDebugMcp: Claude drives your debugger");
    const boxes = [
      [fa.FaTerminal, "Claude Code", "MCP client", C.amber, C.bg],
      [fa.FaServer, "VsDebugMcp", "MCP server", C.purple, C.text],
      [fa.FaDesktop, "Visual Studio", "VS instance", C.surface2, C.text],
      [fa.FaCogs, "Your app", "target process", C.surface2, C.text],
    ];
    const w = 1.85, gap = 0.5;
    let x = 0.5;
    for (let i = 0; i < boxes.length; i++) {
      const [I, head, sub, fill, fg] = boxes[i];
      card(s, x, 1.75, w, 1.6, fill);
      s.addImage({ data: await icon(I, fg), x: x + w / 2 - 0.25, y: 1.92, w: 0.5, h: 0.5 });
      s.addText(head, { x, y: 2.5, w, h: 0.4, fontFace: H, fontSize: 16, bold: true, color: fg, align: "center", margin: 0 });
      s.addText(sub, { x, y: 2.88, w, h: 0.3, fontFace: B, fontSize: 12, italic: true, color: fg, align: "center", margin: 0 });
      if (i < boxes.length - 1) arrow(s, x + w + 0.06, 2.55, x + w + gap - 0.06);
      x += w + gap;
    }
    const tools = ["list_instances", "attach", "set_breakpoint", "launch_process", "read_stack", "evaluate", "detach"];
    let tx = 0.5;
    for (const t of tools) {
      const tw = 0.26 + t.length * 0.084;
      s.addShape("roundRect", { x: tx, y: 3.85, w: tw, h: 0.38, fill: { color: C.surface }, line: { color: C.purple, width: 1 }, rectRadius: 0.19 });
      s.addText(t, { x: tx, y: 3.85, w: tw, h: 0.38, fontFace: "Courier New", fontSize: 10, color: C.text, align: "center", valign: "middle", margin: 0 });
      tx += tw + 0.1;
    }
    s.addText("21 tools — discover, bind, break, inspect, release", {
      x: 0.5, y: 4.45, w: 9, h: 0.35, fontFace: B, fontSize: 14, italic: true, color: C.muted, margin: 0,
    });
    s.addNotes("Claude talks MCP to VsDebugMcp; VsDebugMcp drives the Visual Studio you already have open; VS debugs your app. Three words: VS instance, debug session (Claude's handle on one VS), target process (the app).");
  }

  // 5. Why this approach
  {
    const s = base();
    title(s, "Why this approach");
    const items = [
      [fa.FaMicroscope, "Evidence, not guesses", "Proves the bug from live state"],
      [fa.FaDesktop, "Your VS, your setup", "Reuses the instance you have open"],
      [fa.FaProjectDiagram, "Multi-app", "One debug session per VS instance"],
      [fa.FaShieldAlt, "Safe by default", "Read-only evaluate, explicit consent"],
    ];
    const pos = [[0.5, 1.3], [5.1, 1.3], [0.5, 3.25], [5.1, 3.25]];
    for (let i = 0; i < 4; i++) {
      const [I, head, body] = items[i];
      const [x, y] = pos[i];
      card(s, x, y, 4.4, 1.7);
      await badge(s, I, x + 0.3, y + 0.45, 0.8, i === 0 ? C.amber : C.purple, i === 0 ? C.bg : C.text);
      s.addText(head, { x: x + 1.35, y: y + 0.42, w: 2.9, h: 0.42, fontFace: H, fontSize: 18, bold: true, color: C.text, margin: 0 });
      s.addText(body, { x: x + 1.35, y: y + 0.86, w: 2.9, h: 0.5, fontFace: B, fontSize: 14, color: C.muted, margin: 0, valign: "top" });
    }
    s.addNotes("Reading code gives hypotheses; a stopped process gives facts. No new tooling: your VS. Follows one request across two services. Nothing mutates without explicit consent.");
  }

  // 6. Today's demo
  {
    const s = base();
    title(s, "Today’s demo", "Two apps, two VS instances, one wrong total");
    const apps = [
      [0.5, "Orders.Api", ":5201 · frontend"],
      [5.5, "Pricing.Engine", ":5202 · pricing rules"],
    ];
    for (const [x, head, sub] of apps) {
      card(s, x, 1.7, 4, 1.05, C.surface2);
      s.addImage({ data: await icon(fa.FaDesktop, C.purple), x: x + 0.25, y: 1.95, w: 0.55, h: 0.55 });
      s.addText(head, { x: x + 1.0, y: 1.8, w: 2.9, h: 0.45, fontFace: H, fontSize: 20, bold: true, color: C.text, margin: 0 });
      s.addText(sub, { x: x + 1.0, y: 2.25, w: 2.9, h: 0.35, fontFace: B, fontSize: 14, color: C.muted, margin: 0 });
    }
    arrow(s, 4.6, 2.22, 5.4, C.amber);
    s.addText("HTTP", { x: 4.5, y: 1.85, w: 1, h: 0.3, fontFace: B, fontSize: 11, color: C.amber, align: "center", margin: 0 });

    const orders = [
      [0.5, "Order 1", "widget × 1", "108.00", C.green, "as expected", fa.FaCheckCircle],
      [5.5, "Order 2", "widget × 10 + SAVE20", "108.00 ?", C.red, "should be 756.00", fa.FaQuestionCircle],
    ];
    for (const [x, head, body, total, col, note, I] of orders) {
      card(s, x, 3.1, 4, 1.75);
      s.addText(head, { x: x + 0.3, y: 3.25, w: 2.4, h: 0.38, fontFace: H, fontSize: 16, bold: true, color: C.text, margin: 0 });
      s.addText(body, { x: x + 0.3, y: 3.62, w: 3.4, h: 0.32, fontFace: "Courier New", fontSize: 13, color: C.muted, margin: 0 });
      s.addText(total, { x: x + 0.3, y: 3.98, w: 2.6, h: 0.55, fontFace: H, fontSize: 28, bold: true, color: col, margin: 0 });
      s.addText(note, { x: x + 0.3, y: 4.5, w: 3.4, h: 0.28, fontFace: B, fontSize: 12, italic: true, color: col, margin: 0 });
      s.addImage({ data: await icon(I, col), x: x + 3.25, y: 3.3, w: 0.5, h: 0.5 });
    }
    s.addNotes("Two ASP.NET Core apps, each open in its own VS. Order 1 is right. Order 2 returns a plausible but wrong total. No code changes during the demo. Claude must find why — through the debugger only.");
  }

  // 7. Handoff
  {
    const s = base();
    s.addText("Let’s debug.", { x: 0.5, y: 1.0, w: 9, h: 1.0, fontFace: H, fontSize: 54, bold: true, color: C.text, margin: 0 });
    card(s, 0.5, 2.4, 9, 1.5, "0D0A12");
    s.addText([
      { text: "claude", options: { color: C.muted, breakLine: true } },
      { text: "> ", options: { color: C.amber } },
      { text: "start", options: { color: C.text, bold: true } },
    ], { x: 0.8, y: 2.55, w: 8.4, h: 1.2, fontFace: "Courier New", fontSize: 24, valign: "middle", margin: 0 });
    s.addText("Discover → Bind → Breakpoints → Repro → Inspect → Verdict", {
      x: 0.5, y: 4.3, w: 9, h: 0.4, fontFace: B, fontSize: 16, italic: true, color: C.amber, margin: 0,
    });
    s.addNotes("Switch to the terminal. Type: start. Narrate each step as Claude runs it.");
  }

  // 8. By the numbers
  {
    const s = base();
    title(s, "By the numbers", "Built with Claude Code");
    const stats = [["21", "MCP tools"], ["238", "automated tests"], ["~10", "days"], ["129", "commits"]];
    const w = 2.0, gap = 0.33;
    let x = 0.5;
    for (let i = 0; i < stats.length; i++) {
      const [n, label] = stats[i];
      card(s, x, 1.85, w, 2.3);
      s.addText(n, { x, y: 2.05, w, h: 1.2, fontFace: H, fontSize: 60, bold: true, color: i === 2 ? C.amber : C.purple, align: "center", margin: 0 });
      s.addText(label, { x, y: 3.3, w, h: 0.5, fontFace: B, fontSize: 16, color: C.text, align: "center", margin: 0 });
      x += w + gap;
    }
    s.addText("Oct 19 → Oct 29, 2026 · spec → issues → TDD → live E2E", {
      x: 0.5, y: 4.55, w: 9, h: 0.35, fontFace: B, fontSize: 14, italic: true, color: C.muted, margin: 0,
    });
    s.addNotes("Ten days, AI-assisted end to end: specs, issues, test-first, and real-VS E2E passes. 238 tests run against a fake bridge; live checks run against real VS.");
  }

  // 9. What's next
  {
    const s = base();
    title(s, "What’s next");
    const cards = [
      [fa.FaChartLine, "Live tool-call dashboard", "Every call of a client session, live, grouped by debug session", C.purple, C.text],
      [fa.FaClipboardCheck, "Flow-testing skills", "Flow plans = acceptance criteria the debugger checks · 1 of 3 shipped", C.purple, C.text],
      [fa.FaLightbulb, "Your idea here", "What would you hand to the debugger?", C.amber, C.bg],
    ];
    const w = 2.8, gap = 0.3;
    let x = 0.5;
    for (const [I, head, body, fill, fg] of cards) {
      card(s, x, 1.4, w, 3.4);
      await badge(s, I, x + 0.3, 1.7, 0.85, fill, fg);
      s.addText(head, { x: x + 0.3, y: 2.75, w: w - 0.6, h: 0.75, fontFace: H, fontSize: 18, bold: true, color: C.text, margin: 0, valign: "top" });
      s.addText(body, { x: x + 0.3, y: 3.5, w: w - 0.6, h: 1.15, fontFace: B, fontSize: 14, color: C.muted, margin: 0, valign: "top" });
      x += w + gap;
    }
    s.addNotes("Dashboard: watch the tool calls stream instead of reading a terminal. Flow-testing skills: what you just saw, but written by Claude from your code: flow plans, then a verdict. Third card opens Q&A.");
  }

  // 10. Q&A
  {
    const s = base();
    await badge(s, fa.FaComments, 4.4, 1.0, 1.2, C.purple);
    s.addText("Questions?", { x: 0.5, y: 2.45, w: 9, h: 0.9, fontFace: H, fontSize: 48, bold: true, color: C.text, align: "center", margin: 0 });
    s.addText("¿Preguntas? — en español también", { x: 0.5, y: 3.35, w: 9, h: 0.45, fontFace: B, fontSize: 20, italic: true, color: C.amber, align: "center", margin: 0 });
    s.addText("github.com/SebastianZonta/Unofficial-VisualStudio-MCP", {
      x: 0.5, y: 4.6, w: 9, h: 0.35, fontFace: "Courier New", fontSize: 14, color: C.muted, align: "center", margin: 0,
    });
    s.addNotes("Invite questions, in Spanish is fine.");
  }

  await pres.writeFile({ fileName: OUT });
  console.log("wrote " + OUT);
})();
