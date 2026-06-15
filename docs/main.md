# Acai Documentation Home

![Acai Logo](images/acai-logo.png)

Welcome to the official Acai docs. This page is designed as a lightweight web-style guide with a search bar, concise sections, and links to every chapter.

<div class="search-box">
  <input id="docSearch" type="search" placeholder="Search docs..." />
  <button onclick="runSearch()">Search</button>
</div>

<div id="searchResults"></div>

## Quick links
- [Contents](contents.md)
- [Getting Started](installing/installing.md)
- [Tutorial](tutorial/introduction.md)
- [New in Acai](whatsnew/main.md)
- [Report a Bug](bugs.md)

## Quick start
Acai is a friendly hybrid language with simple keywords like `show`, `set`, `if ... then ... else`, `repeat`, `for`, `make function`, `make class`, and `call`.

Most users begin with the tutorial, then explore the language reference and examples.

![Search the docs](images/docs-search.png)

<script>
const docs = [
  { title: 'Contents', path: 'contents.md', desc: 'Explore all docs pages.' },
  { title: 'Install Acai', path: 'installing/installing.md', desc: 'Step-by-step install instructions.' },
  { title: 'Introduction', path: 'tutorial/introduction.md', desc: 'What Acai is and why it exists.' },
  { title: 'Control Flow', path: 'tutorial/controlflow.md', desc: 'If/else, else if, loops, and decisions.' },
  { title: 'Input / Output', path: 'tutorial/inputoutput.md', desc: 'Read user input and print results.' },
  { title: 'Data and Classes', path: 'tutorial/datastructure.md', desc: 'Variables, classes, and objects.' },
  { title: 'Classes', path: 'tutorial/classes.md', desc: 'Build classes, constructors, and self.' },
  { title: 'Errors', path: 'tutorial/errors.md', desc: 'Common error situations and debugging tips.' },
  { title: 'Changelog', path: 'whatsnew/changelog.md', desc: 'Version notes and release history.' },
];

function runSearch() {
  const query = document.getElementById('docSearch').value.trim().toLowerCase();
  const results = docs.filter(d => d.title.toLowerCase().includes(query) || d.desc.toLowerCase().includes(query));
  const container = document.getElementById('searchResults');
  if (!query) { container.innerHTML = '<p>Type a topic and press search to find docs.</p>'; return; }
  if (results.length === 0) { container.innerHTML = '<p>No results found. Try another keyword.</p>'; return; }
  container.innerHTML = '<ul>' + results.map(r => `<li><a href="${r.path}">${r.title}</a> — ${r.desc}</li>`).join('') + '</ul>';
}
</script>

<style>
.search-box { margin: 1rem 0; }
input[type=search] { width: 70%; max-width: 360px; padding: 0.7rem; border: 1px solid #888; border-radius: 4px; }
button { padding: 0.7rem 1rem; margin-left: 0.5rem; border-radius: 4px; border: 1px solid #777; background: #6f42c1; color: white; }
img { max-width: 220px; margin: 1rem 0; }
</style>
