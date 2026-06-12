// Movie Catalog viewer. Vanilla JS, no build step.
// Fetches movies.json, renders a sortable + searchable table.

(function () {
  "use strict";

  const $ = (s) => document.querySelector(s);
  let rows = [];
  let sortKey = "title";
  let sortAsc = true;

  fetch("movies.json", { cache: "no-cache" })
    .then((r) => {
      if (!r.ok) throw new Error("fetch failed: " + r.status);
      return r.json();
    })
    .then((data) => {
      rows = Array.isArray(data.movies) ? data.movies : [];
      if (data.generated_at) {
        $("#meta").textContent = "Last updated: " + fmtDate(data.generated_at);
      }
      render();
    })
    .catch((err) => {
      $("#tbody").innerHTML = '<tr><td colspan="5" class="error">Could not load movies.json: ' + esc(err.message) + "</td></tr>";
    });

  $("#q").addEventListener("input", render);
  $("#q").addEventListener("keydown", (e) => {
    if (e.key === "Escape" && $("#q").value !== "") {
      $("#q").value = "";
      render();
      e.preventDefault();
    }
  });
  document.querySelectorAll("th[data-key].sortable").forEach((th) => {
    th.addEventListener("click", () => {
      const k = th.dataset.key;
      sortAsc = sortKey === k ? !sortAsc : true;
      sortKey = k;
      updateSortIndicators();
      render();
    });
  });

  function render() {
    const q = $("#q").value.toLowerCase();
    const filtered = rows
      .filter((r) => (r.title || "").toLowerCase().includes(q))
      .sort(compare);

    if (filtered.length === 0) {
      $("#tbody").innerHTML = '<tr><td colspan="5" class="empty">No matches.</td></tr>';
    } else {
      $("#tbody").innerHTML = filtered.map(renderRow).join("");
    }
    $("#count").textContent = filtered.length + " of " + rows.length;
  }

  function compare(a, b) {
    const av = a[sortKey];
    const bv = b[sortKey];
    if (av == null && bv == null) return 0;
    if (av == null) return 1;
    if (bv == null) return -1;
    const cmp = av > bv ? 1 : av < bv ? -1 : 0;
    return cmp * (sortAsc ? 1 : -1);
  }

  function renderRow(r) {
    const genres = (r.genres || []).map(esc).join(", ");
    return "<tr>" +
      "<td>" + esc(r.title) + "</td>" +
      "<td>" + (r.year != null ? r.year : "") + "</td>" +
      "<td>" + fmtRuntime(r.runtime_seconds) + "</td>" +
      "<td>" + genres + "</td>" +
      "<td>" + (r.date_added ? r.date_added.slice(0, 10) : "") + "</td>" +
      "</tr>";
  }

  function updateSortIndicators() {
    document.querySelectorAll("th[data-key]").forEach((th) => {
      th.classList.remove("sort-asc", "sort-desc");
      if (th.dataset.key === sortKey) {
        th.classList.add(sortAsc ? "sort-asc" : "sort-desc");
      }
    });
  }

  function esc(s) {
    return String(s == null ? "" : s).replace(/[&<>"']/g, (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c])
    );
  }

  function fmtRuntime(s) {
    if (s == null || s === 0) return "";
    const m = Math.round(s / 60);
    const h = Math.floor(m / 60);
    const mm = m % 60;
    return h > 0 ? h + "h " + mm + "m" : mm + "m";
  }

  function fmtDate(iso) {
    try {
      const d = new Date(iso);
      if (isNaN(d.getTime())) return iso;
      return d.toISOString().slice(0, 16).replace("T", " ") + " UTC";
    } catch (e) {
      return iso;
    }
  }
})();
