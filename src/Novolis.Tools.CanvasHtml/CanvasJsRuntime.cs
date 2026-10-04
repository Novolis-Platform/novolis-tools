namespace Novolis.Tools.CanvasHtml;

/// <summary>JavaScript prelude that stands in for <c>cursor/canvas</c>.</summary>
internal static class CanvasJsRuntime
{
    internal const string Source = """
        var __category = {
          gray: "#8A8A8A",
          purple: "#7B64B8",
          green: "#1F8A65",
          yellow: "#C4A035",
          cyan: "#2A9A8A",
          pink: "#C85898",
          blue: "#2E79B5",
          orange: "#C06028",
          red: "#C04848"
        };
        var __host = {
          kind: "dark",
          text: {
            primary: "#E8E8E8",
            secondary: "#B5B5B5",
            tertiary: "#8E8E8E",
            quaternary: "#6A6A6A",
            link: "#70B0D8",
            onAccent: "#101010"
          },
          bg: { editor: "#181818", chrome: "#141414", elevated: "#202020" },
          fill: {
            primary: "rgba(255,255,255,0.14)",
            secondary: "rgba(255,255,255,0.09)",
            tertiary: "rgba(255,255,255,0.05)",
            quaternary: "rgba(255,255,255,0.03)"
          },
          stroke: {
            primary: "rgba(255,255,255,0.22)",
            secondary: "rgba(255,255,255,0.14)",
            tertiary: "rgba(255,255,255,0.08)",
            focused: "#70B0D8"
          },
          accent: { primary: "#70B0D8", control: "#2E79B5", controlHover: "#3B88C4" },
          diff: {
            insertedLine: "rgba(31,138,101,0.25)",
            removedLine: "rgba(192,72,72,0.25)",
            stripAdded: "#1F8A65",
            stripRemoved: "#C04848"
          },
          category: __category
        };
        __host.tokens = __host;
        __host.palette = __host.text;

        if (typeof Object.fromEntries !== "function") {
          Object.fromEntries = function (entries) {
            var result = {};
            var list = Array.isArray(entries) ? entries : Array.from(entries);
            for (var i = 0; i < list.length; i++) result[list[i][0]] = list[i][1];
            return result;
          };
        }

        function useHostTheme() { return __host; }
        function useState(initial) {
          var value = typeof initial === "function" ? initial() : initial;
          return [value, function () {}];
        }
        function useMemo(factory) { return factory(); }
        function useRef(initial) { return { current: initial }; }
        function useEffect() {}
        function useCanvasState(key, initial) { return useState(initial); }
        function useCanvasAction() { return function () {}; }
        function mergeStyle(base, override) {
          var result = {};
          var source;
          for (var pass = 0; pass < 2; pass++) {
            source = pass === 0 ? base : override;
            if (!source) continue;
            for (var key in source) {
              if (Object.prototype.hasOwnProperty.call(source, key)) result[key] = source[key];
            }
          }
          return result;
        }

        function __h(type, props) {
          var children = [];
          function flat(value) {
            if (value == null || value === false || value === true) return;
            if (Array.isArray(value)) {
              for (var i = 0; i < value.length; i++) flat(value[i]);
              return;
            }
            children.push(value);
          }
          for (var a = 2; a < arguments.length; a++) flat(arguments[a]);
          var next = {};
          if (props) {
            for (var key in props) {
              if (Object.prototype.hasOwnProperty.call(props, key) && key !== "children") next[key] = props[key];
            }
            if (children.length === 0 && props.children != null) flat(props.children);
          }
          next.children = children;
          return { type: type, props: next };
        }

        function __Fragment(props) { return props.children; }

        function __props() {
          var result = {};
          for (var i = 0; i < arguments.length; i++) {
            var part = arguments[i];
            if (!part) continue;
            if (part.__spread) {
              var source = part.value || {};
              for (var key in source) {
                if (Object.prototype.hasOwnProperty.call(source, key)) result[key] = source[key];
              }
            } else {
              for (var name in part) {
                if (Object.prototype.hasOwnProperty.call(part, name)) result[name] = part[name];
              }
            }
          }
          return result;
        }

        var __unitless = {
          opacity: 1, zIndex: 1, fontWeight: 1, lineHeight: 1, flex: 1, flexGrow: 1, flexShrink: 1,
          order: 1, zoom: 1, fillOpacity: 1, strokeOpacity: 1
        };

        function __css(style) {
          if (style == null || style === false) return "";
          if (typeof style === "string") return style;
          var parts = [];
          for (var key in style) {
            if (!Object.prototype.hasOwnProperty.call(style, key)) continue;
            var value = style[key];
            if (value == null || value === false) continue;
            var cssKey = key.replace(/[A-Z]/g, function (letter) { return "-" + letter.toLowerCase(); });
            if (typeof value === "number" && !__unitless[key]) value = value + "px";
            parts.push(cssKey + ":" + value);
          }
          return parts.join(";");
        }

        function __esc(value) {
          return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
        }

        function __assignStyle(extra, style) {
          var result = {};
          var sources = [extra || {}, style || {}];
          for (var s = 0; s < sources.length; s++) {
            for (var key in sources[s]) {
              if (Object.prototype.hasOwnProperty.call(sources[s], key)) result[key] = sources[s][key];
            }
          }
          return result;
        }

        function __flexAlign(value) {
          if (value === "start") return "flex-start";
          if (value === "end") return "flex-end";
          return value || "stretch";
        }

        function __flexJustify(value) {
          if (value === "start") return "flex-start";
          if (value === "end") return "flex-end";
          if (value === "space-between") return "space-between";
          return value || "flex-start";
        }

        function Stack(props) {
          var style = { display: "flex", flexDirection: "column" };
          if (props.gap != null) style.gap = props.gap;
          return __h("div", { className: "cv-stack", style: __assignStyle(style, props.style) }, props.children);
        }
        function Row(props) {
          var style = {
            display: "flex",
            flexDirection: "row",
            alignItems: __flexAlign(props.align || "center"),
            justifyContent: __flexJustify(props.justify)
          };
          if (props.gap != null) style.gap = props.gap;
          if (props.wrap) style.flexWrap = "wrap";
          return __h("div", { className: "cv-row", style: __assignStyle(style, props.style) }, props.children);
        }
        function Grid(props) {
          var columns = typeof props.columns === "number"
            ? "repeat(" + props.columns + ", minmax(0, 1fr))"
            : props.columns;
          var style = { display: "grid", gridTemplateColumns: columns, alignItems: __flexAlign(props.align) };
          if (props.gap != null) style.gap = props.gap;
          return __h("div", { className: "cv-grid", style: __assignStyle(style, props.style) }, props.children);
        }
        function Divider(props) {
          return __h("hr", { className: "cv-divider", style: props.style });
        }
        function Spacer() {
          return __h("div", { className: "cv-spacer" });
        }
        function H1(props) { return __h("h1", { style: props.style }, props.children); }
        function H2(props) { return __h("h2", { style: props.style }, props.children); }
        function H3(props) { return __h("h3", { style: props.style }, props.children); }
        function Text(props) {
          var tone = props.tone || "primary";
          var size = props.size || "body";
          var weight = props.weight ? " cv-w-" + props.weight : "";
          var italic = props.italic ? " cv-italic" : "";
          return __h(props.as || "p", {
            className: "cv-text cv-tone-" + tone + " cv-size-" + size + weight + italic,
            style: props.style
          }, props.children);
        }
        function Code(props) {
          return __h("code", { style: props.style }, props.children);
        }
        function Link(props) {
          return __h("a", { href: props.href, style: props.style }, props.children);
        }
        function Card(props) {
          var plain = props.variant === "borderless" ? " cv-card-plain" : "";
          return __h("section", { className: "cv-card" + plain, style: props.style }, props.children);
        }
        function CardHeader(props) {
          return __h("header", { className: "cv-card-h", style: props.style },
            __h("span", { className: "cv-card-title" }, props.children),
            props.trailing != null ? __h("span", { className: "cv-card-trail" }, props.trailing) : null);
        }
        function CardBody(props) {
          return __h("div", { className: "cv-card-b", style: props.style }, props.children);
        }
        function Button(props) {
          return __h("button", {
            type: props.type || "button",
            disabled: !!props.disabled,
            className: "cv-button cv-button-" + (props.variant || "secondary"),
            style: props.style
          }, props.children);
        }
        function Pill(props) {
          var on = props.active ? " cv-pill-on" : "";
          var small = props.size === "sm" ? " cv-pill-sm" : "";
          return __h("span", { className: "cv-pill" + on + small, title: props.title, style: props.style },
            props.leadingContent, props.children,
            props.keyboardHint ? __h("kbd", null, props.keyboardHint) : null);
        }
        function Stat(props) {
          var tone = props.tone ? " cv-tone-" + props.tone : "";
          return __h("div", { className: "cv-stat" + tone, style: props.style },
            __h("div", { className: "cv-stat-v" }, props.value),
            __h("div", { className: "cv-stat-l" }, props.label));
        }
        function Callout(props) {
          return __h("aside", { className: "cv-callout cv-tone-" + (props.tone || "info"), style: props.style },
            props.title != null ? __h("div", { className: "cv-callout-title" }, props.title) : null,
            __h("div", null, props.children));
        }
        function Table(props) {
          var headers = props.headers || [];
          var rows = props.rows || [];
          var align = props.columnAlign || [];
          var tones = props.rowTone || [];
          var head = __h("tr", null, headers.map(function (cell, index) {
            return __h("th", { style: { textAlign: align[index] || "left" } }, cell);
          }));
          var body = rows.map(function (row, rowIndex) {
            var tone = tones[rowIndex] ? "cv-row-" + tones[rowIndex] : "";
            return __h("tr", { className: tone }, headers.map(function (_, index) {
              var cell = row && index < row.length ? row[index] : "";
              return __h("td", { style: { textAlign: align[index] || "left" } }, cell == null ? "" : cell);
            }));
          });
          var table = __h("table", { className: "cv-table" + (props.striped ? " cv-striped" : "") },
            __h("thead", null, head), __h("tbody", null, body));
          if (props.framed === false) return table;
          return __h("div", { className: "cv-table-frame", style: props.style }, table);
        }
        function UsageBar(props) {
          var segments = props.segments || [];
          var total = props.total || 0;
          var order = ["blue", "purple", "green", "orange", "yellow", "cyan", "pink", "red", "gray"];
          var sum = 0;
          for (var i = 0; i < segments.length; i++) sum += segments[i].value > 0 ? segments[i].value : 0;
          var bars = segments.map(function (segment, index) {
            var width = total > 0 ? (Math.max(segment.value || 0, 0) / total) * 100 : 0;
            var color = __category[segment.color] || __category[order[index % order.length]];
            return __h("div", { style: { width: width + "%", background: color }, title: segment.id });
          });
          var remainder = Math.max(0, total - sum);
          if (total > 0 && remainder > 0) {
            bars.push(__h("div", { className: "cv-usage-rest", style: { width: (remainder / total) * 100 + "%" } }));
          }
          var legend = segments.map(function (segment, index) {
            var color = __category[segment.color] || __category[order[index % order.length]];
            return __h("span", null, __h("i", { style: { background: color } }), " " + segment.id + " " + segment.value);
          });
          return __h("div", { className: "cv-usage", style: props.style },
            (props.topLeftLabel || props.topRightLabel)
              ? __h("div", { className: "cv-usage-labels" },
                  __h("span", null, props.topLeftLabel || ""),
                  __h("span", null, props.topRightLabel || ""))
              : null,
            __h("div", { className: "cv-usage-bar" }, bars),
            __h("div", { className: "cv-usage-legend" }, legend));
        }
        function TodoList(props) {
          var todos = props.todos || [];
          return __h("ul", { className: "cv-todo", style: props.style }, todos.map(function (todo) {
            return __h("li", { className: "cv-todo-" + (todo.status || "pending") },
              __h("span", { className: "cv-todo-status" }, todo.status || "pending"),
              __h("span", null, todo.content || ""));
          }));
        }
        function TodoListCard(props) {
          return __h("section", { className: "cv-card" }, TodoList(props));
        }
        function CollapsibleSection(props) {
          return __h("details", { open: true, className: "cv-fold", style: props.style },
            __h("summary", null, props.leading, props.title, props.count != null ? " (" + props.count + ")" : "", props.trailing),
            __h("div", { className: "cv-fold-body" }, props.children));
        }
        function Swatch(props) {
          var color = __category[props.color] || props.color || __category.gray;
          return __h("i", { className: "cv-swatch", style: __assignStyle({ background: color }, props.style) });
        }
        function BarChart(props) { return __chart("Bar chart", props); }
        function LineChart(props) { return __chart("Line chart", props); }
        function PieChart(props) { return __chart("Pie chart", props); }
        function __chart(title, props) {
          var series = props.series || props.data || [];
          return __h("figure", { className: "cv-card" },
            __h("figcaption", { className: "cv-card-h" }, title),
            __h("pre", { className: "cv-pre" }, JSON.stringify(series, null, 2)));
        }
        function DiffView(props) {
          var lines = props.lines || [];
          return __h("pre", { className: "cv-pre" }, lines.map(function (line) {
            var kind = line.type || line.kind || "";
            var text = line.text || line.content || line.line || "";
            return kind + " " + text;
          }).join("\n"));
        }
        function DiffStats(props) {
          return __h("span", { className: "cv-text cv-size-small" }, "+" + (props.additions || 0) + " / -" + (props.deletions || 0));
        }
        function Checkbox(props) { return __h("label", null, __h("input", { type: "checkbox", checked: !!props.checked, disabled: true }), " ", props.children || props.label); }
        function Toggle(props) { return Checkbox(props); }
        function TextInput(props) { return __h("input", { type: "text", value: props.value || "", disabled: true, style: props.style }); }
        function TextArea(props) { return __h("textarea", { disabled: true, style: props.style }, props.value || props.children || ""); }
        function Select(props) {
          var options = props.options || [];
          return __h("select", { disabled: true }, options.map(function (option) {
            var label = option.label || option.value || option;
            return __h("option", null, label);
          }));
        }
        function IconButton(props) { return Button(props); }

        function computeDAGLayout(opts) {
          opts = opts || {};
          var nodes = opts.nodes || [];
          var edges = opts.edges || [];
          var nodeWidth = opts.nodeWidth || 160;
          var nodeHeight = opts.nodeHeight || 40;
          var rankGap = opts.rankGap == null ? 64 : opts.rankGap;
          var nodeGap = opts.nodeGap == null ? 48 : opts.nodeGap;
          var padding = opts.padding == null ? 24 : opts.padding;
          var horizontal = opts.direction === "horizontal";
          var ids = [];
          var indeg = {};
          var outgoing = {};
          for (var i = 0; i < nodes.length; i++) {
            ids.push(nodes[i].id);
            indeg[nodes[i].id] = 0;
            outgoing[nodes[i].id] = [];
          }
          for (var e = 0; e < edges.length; e++) {
            var edge = edges[e];
            if (!outgoing[edge.from]) outgoing[edge.from] = [];
            outgoing[edge.from].push(edge.to);
            indeg[edge.to] = (indeg[edge.to] || 0) + 1;
          }
          var rank = {};
          var queue = [];
          for (var q = 0; q < ids.length; q++) {
            rank[ids[q]] = 0;
            if (!indeg[ids[q]]) queue.push(ids[q]);
          }
          var head = 0;
          while (head < queue.length) {
            var id = queue[head++];
            var next = outgoing[id] || [];
            for (var k = 0; k < next.length; k++) {
              var to = next[k];
              if ((rank[id] || 0) + 1 > (rank[to] || 0)) rank[to] = (rank[id] || 0) + 1;
              indeg[to]--;
              if (indeg[to] === 0) queue.push(to);
            }
          }
          var groups = [];
          for (var g = 0; g < ids.length; g++) {
            var groupRank = rank[ids[g]] || 0;
            while (groups.length <= groupRank) groups.push([]);
            groups[groupRank].push(ids[g]);
          }
          var positioned = [];
          var byId = {};
          for (var rg = 0; rg < groups.length; rg++) {
            var row = groups[rg];
            for (var o = 0; o < row.length; o++) {
              var nodeId = row[o];
              var x = horizontal ? padding + rg * (nodeWidth + rankGap) : padding + o * (nodeWidth + nodeGap);
              var y = horizontal ? padding + o * (nodeHeight + nodeGap) : padding + rg * (nodeHeight + rankGap);
              var placed = { id: nodeId, x: x, y: y, rank: rg, order: o };
              positioned.push(placed);
              byId[nodeId] = placed;
            }
          }
          var laid = [];
          for (var ei = 0; ei < edges.length; ei++) {
            var from = byId[edges[ei].from];
            var toNode = byId[edges[ei].to];
            if (!from || !toNode) continue;
            laid.push({
              from: edges[ei].from,
              to: edges[ei].to,
              sourceX: horizontal ? from.x + nodeWidth : from.x + nodeWidth / 2,
              sourceY: horizontal ? from.y + nodeHeight / 2 : from.y + nodeHeight,
              targetX: horizontal ? toNode.x : toNode.x + nodeWidth / 2,
              targetY: horizontal ? toNode.y + nodeHeight / 2 : toNode.y,
              isBackEdge: false
            });
          }
          var width = padding;
          var height = padding;
          for (var p = 0; p < positioned.length; p++) {
            var right = positioned[p].x + nodeWidth + padding;
            var bottom = positioned[p].y + nodeHeight + padding;
            if (right > width) width = right;
            if (bottom > height) height = bottom;
          }
          return { nodes: positioned, edges: laid, ranks: [], direction: horizontal ? "horizontal" : "vertical", width: width, height: height };
        }

        var __svgKeep = { viewBox: 1, preserveAspectRatio: 1 };
        var __void = { area: 1, base: 1, br: 1, col: 1, embed: 1, hr: 1, img: 1, input: 1, link: 1, meta: 1, source: 1, track: 1, wbr: 1 };

        function __attrName(name) {
          if (name === "className") return "class";
          if (name === "htmlFor") return "for";
          if (__svgKeep[name]) return name;
          return name.replace(/[A-Z]/g, function (letter) { return "-" + letter.toLowerCase(); });
        }

        function __element(node) {
          var type = node.type;
          var props = node.props || {};
          var attrs = "";
          var style = __css(props.style);
          if (style) attrs += " style=\"" + __esc(style) + "\"";
          for (var key in props) {
            if (!Object.prototype.hasOwnProperty.call(props, key)) continue;
            if (key === "children" || key === "style" || key === "key" || key === "ref") continue;
            if (key.indexOf("on") === 0 && typeof props[key] === "function") continue;
            var value = props[key];
            if (value == null || value === false) continue;
            if (value === true) {
              attrs += " " + __attrName(key);
              continue;
            }
            if (typeof value === "object") continue;
            attrs += " " + __attrName(key) + "=\"" + __esc(value) + "\"";
          }
          if (type === "svg" && !props.xmlns) attrs += " xmlns=\"http://www.w3.org/2000/svg\"";
          var inner = "";
          var children = props.children || [];
          for (var i = 0; i < children.length; i++) inner += __render(children[i]);
          if (__void[type]) return "<" + type + attrs + ">";
          return "<" + type + attrs + ">" + inner + "</" + type + ">";
        }

        function __render(node) {
          if (node == null || node === false || node === true) return "";
          if (Array.isArray(node)) {
            var html = "";
            for (var i = 0; i < node.length; i++) html += __render(node[i]);
            return html;
          }
          if (typeof node === "string" || typeof node === "number") return __esc(node);
          if (typeof node.type === "function") return __render(node.type(node.props || {}));
          if (typeof node.type === "string") return __element(node);
          return "";
        }
        """;
}
