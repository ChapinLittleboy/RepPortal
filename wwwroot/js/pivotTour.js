// ============================================================
// pivotTour.js  —  Shepherd.js guided tour for
//                  Pivot Table Sales by Item (Rep Portal)
//
// Dependencies:
//   Shepherd.js v14+  (loaded via CDN or npm)
//   shepherdTheme     (inline styles below — no separate CSS file needed)
//
// Wired via IJSRuntime from PivotTablePage.razor.
// Entry point:  window.pivotTour.start()
// ============================================================

window.pivotTour = (() => {

    // ----------------------------------------------------------
    // THEME — injected once into <head>
    // ----------------------------------------------------------
    const THEME_ID = 'pivot-tour-theme';

    function injectTheme() {
        if (document.getElementById(THEME_ID)) return;
        const style = document.createElement('style');
        style.id = THEME_ID;
        style.textContent = `
            /* ── Shepherd container ── */
            .shepherd-theme-portal {
                --portal-bg:      #0f172a;   /* main popup background */
                --portal-border:  #2563eb;   /* accent color */
                --portal-text:    #f8fafc;   /* main text */
                --portal-muted:   #94a3b8;   /* subtle text */
                --portal-btn-bg:  #2563eb;   /* primary button */
                --portal-btn-txt: #ffffff;
                --portal-btn-sec: #1e293b;   /* secondary button */
                font-family: 'Segoe UI', system-ui, sans-serif;
            }

            .shepherd-theme-portal .shepherd-title {
                font-size: 13px;
                font-weight: 700;
                letter-spacing: .06em;
                text-transform: uppercase;
                color: #fecaca;
            }

            .shepherd-theme-portal .shepherd-cancel-icon {
                color: var(--portal-muted);
                background: transparent;
                border: none;
                font-size: 18px;
                cursor: pointer;
                line-height: 1;
            }
            .shepherd-theme-portal .shepherd-cancel-icon:hover { color: #fff; }

            .shepherd-theme-portal .shepherd-element,
            .shepherd-theme-portal .shepherd-element .shepherd-content,
            .shepherd-theme-portal .shepherd-element .shepherd-text,
            .shepherd-theme-portal .shepherd-element .shepherd-footer {
                background: #1e293b !important;
                color: #ffffff !important;
            }

            .shepherd-theme-portal .shepherd-element {
                border: 1px solid var(--portal-border);
                border-radius: 14px;
                box-shadow: 0 18px 50px rgba(0,0,0,.45);
                max-width: 360px;
            }

.shepherd-theme-portal .shepherd-header {
    background: #991b1b !important;
    padding: 14px 16px 0;
    border-bottom: 1px solid #7f1d1d;
    margin-bottom: 2px;
}

            .shepherd-theme-portal .shepherd-text {
                padding: 12px 16px;
                font-size: 14px;
                line-height: 1.65;
            }

            .shepherd-theme-portal .shepherd-text p { margin: 0 0 8px; }
            .shepherd-theme-portal .shepherd-text p:last-child { margin-bottom: 0; }

            .shepherd-theme-portal .shepherd-text .tip {
                display: flex;
                gap: 8px;
                background: #1e3a5f;
                border-left: 3px solid var(--portal-border);
                border-radius: 4px;
                padding: 8px 10px;
                font-size: 12.5px;
                color: #93c5fd;
                margin-top: 10px;
            }

            .shepherd-theme-portal .shepherd-text .warn {
                display: flex;
                gap: 8px;
                background: #3b2a10;
                border-left: 3px solid #f59e0b;
                border-radius: 4px;
                padding: 8px 10px;
                font-size: 12.5px;
                color: #fcd34d;
                margin-top: 10px;
            }

            .shepherd-theme-portal .shepherd-footer {
                padding: 10px 16px 14px;
                display: flex;
                justify-content: space-between;
                align-items: center;
                border-top: 1px solid #334155;
            }

            .shepherd-theme-portal .shepherd-progress {
                font-size: 11px;
                color: var(--portal-muted);
                user-select: none;
            }

            .shepherd-theme-portal .shepherd-button {
                border: none;
                border-radius: 6px;
                padding: 6px 14px;
                font-size: 13px;
                font-weight: 600;
                cursor: pointer;
                transition: opacity .15s;
            }
            .shepherd-theme-portal .shepherd-button:hover {
    opacity: .92;
    transform: translateY(-1px);
}

            .shepherd-theme-portal .shepherd-button-primary {
                background: var(--portal-btn-bg);
                color: var(--portal-btn-txt);
            }

            .shepherd-theme-portal .shepherd-button-secondary {
                background: var(--portal-btn-sec);
                color: var(--portal-text);
            }

            /* Arrow */
            .shepherd-theme-portal .shepherd-arrow:before {
    background: var(--portal-bg);
    border: 1px solid var(--portal-border);
}

            /* Highlight ring on attached elements */
            .shepherd-highlight-pulse {
                   outline: 2px solid var(--portal-border) !important;
                    outline-offset: 4px !important;
                border-radius: 6px;
                box-shadow: 0 0 0 6px rgba(37,99,235,.15);
                transition: all .2s ease;
            }
        `;
        document.head.appendChild(style);
    }

    // ----------------------------------------------------------
    // HELPERS
    // ----------------------------------------------------------

    /** Build the step-counter string shown in the footer */
    function progress(tour, current) {
        return `<span class="shepherd-progress">Step ${current} of ${tour.steps.length}</span>`;
    }

    /** Resolve a target passed as a selector string or a function returning an element */
    function resolveTarget(target) {
        if (!target) return null;
        if (typeof target === 'function') return target();
        return document.querySelector(target);
    }

    /** Standard back / next button pair */
    function navButtons(tour, stepIndex) {
        const isFirst = stepIndex === 0;
        const isLast = stepIndex === tour.steps.length - 1;
        const btns = [];

        if (!isFirst) btns.push({
            text: '← Back',
            classes: 'shepherd-button-secondary',
            action() { tour.back(); }
        });

        btns.push({
            text: isLast ? 'Finish Tour ✓' : 'Next →',
            classes: 'shepherd-button-primary',
            action() { isLast ? tour.complete() : tour.next(); }
        });

        return btns;
    }

    // ----------------------------------------------------------
    // STEP DEFINITIONS
    // ----------------------------------------------------------
    // attachTo.element  = CSS selector for the highlighted element
    // attachTo.on       = Popper placement (bottom, top, left, right, auto)
    //
    // Syncfusion PivotView standard selectors used:
    //   #PivotView                    — outer pivot container
    //   #PivotView .e-pivot-toolbar   — Syncfusion toolbar
    //   #PivotView .e-toggle-field-list — Field List toggle button (⊞)
    //   #PivotView .e-grouping-bar    — the row/column chip bar
    //   #PivotView_grid .e-headercell — first column header cell
    //   #PivotView .e-expand          — any expand arrow (first match)
    //
    // NOTE: Export and Save As use Syncfusion's standard toolbar CSS classes:
    //   .e-export         — Export menu icon (inside .e-menu-item)
    //   .e-saveas-report  — Save As button (only present when Features:PivotLayouts = true)
    // ----------------------------------------------------------

    function buildSteps(tour) {
        // `popupMarginTop` / `popupMarginLeft` are used for fine-grained visual tweaks
        // when Popper placement alone doesn't land a step exactly where we want it.
        const S = (n, title, html, target, placement, extraButtons, beforeShowPromise, offset = [0, 12], popupMarginTop = 0, popupMarginLeft = 0) => {
            const step = {
                id: `step-${n}`,
                title,
                text: `${html}<div style="height:2px"></div>`,
                attachTo: target ? { element: target, on: placement || 'auto' } : undefined,
                scrollTo: { behavior: 'smooth', block: 'center' },
                cancelIcon: { enabled: true },
                classes: 'shepherd-theme-portal',
                buttons: extraButtons || navButtons(tour, n - 1),
                when: {
                    show() {
                        if (popupMarginTop && this.el) {
                            this.el.style.marginTop = `${popupMarginTop}px`;
                        }
                        if (popupMarginLeft && this.el) {
                            this.el.style.marginLeft = `${popupMarginLeft}px`;
                        }
                        const footer = this.el?.querySelector('.shepherd-footer');
                        if (footer && !footer.querySelector('.shepherd-progress')) {
                            footer.insertAdjacentHTML('afterbegin', progress(tour, n));
                        }
                        const resolvedTarget = resolveTarget(target);
                        if (resolvedTarget) {
                            resolvedTarget.classList.add('shepherd-highlight-pulse');
                        }
                    },
                    hide() {
                        if (this.el) {
                            this.el.style.marginTop = '';
                            this.el.style.marginLeft = '';
                        }
                        const resolvedTarget = resolveTarget(target);
                        if (resolvedTarget) {
                            resolvedTarget.classList.remove('shepherd-highlight-pulse');
                        }
                    }
                }, popperOptions: {
                    modifiers: [
                        {
                            name: 'offset',
                            options: {
                                offset: offset
                            }
                        },
                        {
                            name: 'computeStyles',
                            options: {
                                adaptive: false
                            }
                        }
                    ]
                }
            };
            // Only set beforeShowPromise if provided — Shepherd v14 throws on null
            if (beforeShowPromise) step.beforeShowPromise = beforeShowPromise;
            return step;
        };

        return [

            // ── Step 1: Welcome ──────────────────────────────────────
            S(1,
                'Welcome to the Tour',
                `<p>This short tour walks you through the <strong>Pivot Table Sales by Item</strong> page in about 5 minutes.</p>
                 <p>Use <strong>Next →</strong> to advance, <strong>← Back</strong> to revisit a step, or press <strong>✕</strong> to exit at any time.</p>`,
                '#PivotView',
                'bottom',
                [{
                    text: 'Start Tour →',
                    classes: 'shepherd-button-primary',
                    action() { tour.next(); }
                }]
            ),

            // ── Step 2: Orient — Rows ────────────────────────────────
            S(2,
                'The Rows — Your Customers & Products',
                `<p>Down the left side you'll see your <strong>customer accounts</strong>, like <em>40607 – Iron Industries LLC 029</em>. Under each account are the <strong>products</strong> they ordered.</p>
                 <p>There are three drill-down levels here: Company → Product → Ship-To location.</p>`,
                '#PivotView .e-rowsheader',
                'right',
            ),

            // ── Step 3: Orient — Columns ─────────────────────────────
            S(3,
                'The Columns — Fiscal Years',
                `<p>Across the top you'll see <strong>fiscal years</strong> (FY2023, FY2024…). Right now each year is <em>collapsed</em> — you're seeing the full-year total.</p>
                 <div class="tip">💡 Chapin's fiscal year runs Sep–Aug. FY2023 starts September 2022.</div>`,
                '#PivotView .e-columnsheader',
                'bottom',
                undefined,
                undefined,
                [-60, 30],
                24,
                24
            ),

            // ── Step 4: Expand a Year ────────────────────────────────
            S(4,
                'Drill In — Expand a Year',
                `<p>Click the <strong>›</strong> arrow next to any fiscal year to expand it into quarters. Click a quarter's arrow to see individual months.</p>
                 <p>Click the same arrow again to collapse back to the summary.</p>
                 <div class="tip">💡 Think of it like a folder — click to open, click again to close.</div>`,
                '#PivotView .e-expand',
                'top',
                undefined,
                undefined,
                [0, 24],
                20
            ),

            // ── Step 5: Expand a Customer Row ───────────────────────
            S(5,
                'Drill In — Customer Ship-To Locations',
                `<p>The <strong>row arrows</strong> work the same way. Click › next to a product row to see the individual <strong>ship-to locations</strong> receiving that product.</p>
                 <p>This is useful when a customer has multiple warehouses — you can see exactly which site is ordering what.</p>`,
                '#PivotView .e-rowsheader .e-expand',
                'left',
                undefined,
                undefined,
                [0, 12],
                0,
                144
            ),

            // ── Step 6: Sorting ──────────────────────────────────────
            S(6,
                'Sorting — Find Your Top Performers',
                `<p>Click any <strong>Amount</strong> or <strong>Qty</strong> column header to sort the table by that value. Click again to reverse the sort.</p>
                 <div class="warn">⚠ Accounts with zero sales in a year may float to the top when sorting descending. Scroll past them — your real top performers will be the first rows with dollar amounts showing.</div>`,
                '#PivotView .e-valuesheader .e-headercelldiv',
                'bottom',
                undefined,
                undefined,
                [0, 12],
                96
            ),

            // ── Step 7: Filter ───────────────────────────────────────
            S(7,
                'Filtering — Focus on One Account',
                `<p>Click the <strong>funnel icon (▾)</strong> next to the CompanyName label to filter the table down to specific customers.</p>
                 <p>Uncheck the accounts you don't need, click OK, and the entire table — including totals — rebuilds automatically.</p>
                 <div class="tip">💡 Perfect for a customer meeting — filter to just that account before you walk in.</div>`,
                '#PivotView .e-group-rows .e-pivot-button',
                'bottom',
                undefined,
                undefined,
                [0, 12],
                0,
                36
            ),

            // ── Step 8: Field List ───────────────────────────────────
            S(8,
                'Field List — Customize Your View',
                `<p>The <strong>⊞ grid icon</strong> (top-right of the pivot table) opens the <strong>Field List</strong> — the control panel for the table.</p>
                 <p>From here you can add or remove fields and rearrange the <strong>Rows</strong>, <strong>Columns</strong>, and <strong>Values</strong> zones.</p>`,
                '#PivotView .e-toolbar-fieldlist',
                'bottom',
                undefined,
                undefined,
                [0, 12],
                36
            ),

            // ── Step 9: Reorder Rows ─────────────────────────────────
            S(9,
                'Field List — Reorder Row Grouping',
                `<p>Inside the Field List, the <strong>Rows zone</strong> shows chips stacked top-to-bottom: CompanyName → Item-Description → ShipTo.</p>
                 <p>Drag any chip up or down to change the nesting order. The table rebuilds instantly.</p>
                 <div class="tip">💡 Moving ShipTo above Item-Description lets you see warehouse-first, then products within each location.</div>`,
                '#PivotView .e-group-rows',
                'right'
            ),

            // ── Step 10: Save Layout ─────────────────────────────────
            S(10,
                'Save Your Layout',
                `<p>Once your table is set up exactly the way you want, click <strong>Save As</strong> in the toolbar and give it a name.</p>
                 <p>Next time, open the <strong>Report List</strong> dropdown and load your saved layout in one click.</p>
                 <div class="tip">💡 Great for weekly account reviews — set it up once, load it every week.</div>`,
                '#PivotView .e-saveas-report',
                'bottom',
                undefined,
                () => new Promise(resolve => {
                    if (!document.querySelector('#PivotView .e-saveas-report')) {
                        tour.next(); // PivotLayouts feature is disabled — skip this step
                    }
                    resolve();
                }),
                [0, 12],
                30
            ),

            // ── Step 11: Export ──────────────────────────────────────
            S(11,
                'Export to Excel or CSV',
                `<p>Click the <strong>Export icon (⤴)</strong> in the toolbar to download the current view as an Excel or CSV file.</p>
                 <p>The export captures exactly what you see — whatever rows are expanded and whatever filters are applied.</p>`,
                '#PivotView .e-export',
                'bottom',
                undefined,
                undefined,
                [0, 12],
                30
            ),

            // ── Step 12: Done ────────────────────────────────────────
            S(12,
                'You\'re All Set! 🎉',
                `<p>That covers the full Pivot Table Sales by Item page. Here's a quick recap:</p>
                 <p>• <strong>Expand arrows</strong> — drill into years, quarters, months, and ship-to locations<br>
                    • <strong>Sort</strong> — click any column header<br>
                    • <strong>Filter</strong> — funnel icon on CompanyName<br>
                    • <strong>Field List ⊞</strong> — add/remove/reorder fields<br>
                    • <strong>Save As</strong> — save your layout for next time<br>
                    • <strong>Export ⤴</strong> — download to Excel or CSV</p>`,
                '#PivotView',
                'top',
                [{
                    text: '← Back',
                    classes: 'shepherd-button-secondary',
                    action() { tour.back(); }
                }, {
                    text: 'Finish Tour ✓',
                    classes: 'shepherd-button-primary',
                    action() { tour.complete(); }
                }]
            )
        ];
    }

    // ----------------------------------------------------------
    // PUBLIC API
    // ----------------------------------------------------------
    function start() {
        injectTheme();

        // Load Shepherd from CDN if not already present
        function launchTour() {
            const tour = new Shepherd.Tour({
                useModalOverlay: true,
                defaultStepOptions: {
                    modalOverlayOpeningPadding: 6,
                    modalOverlayOpeningRadius: 6,
                    scrollTo: { behavior: 'smooth', block: 'center' }
                }
            });

            buildSteps(tour).forEach(step => tour.addStep(step));

            tour.on('complete', () => {
                // Optionally store completion in localStorage so the
                // floating button can show a checkmark on next visit
                try { localStorage.setItem('pivotTourComplete', '1'); } catch { }
            });

            tour.start();
        }

        if (typeof Shepherd !== 'undefined') {
            launchTour();
            return;
        }

        // Load self-hosted Shepherd (place files in wwwroot/lib/shepherd/)
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = '/lib/shepherd/shepherd.css';
        document.head.appendChild(link);

        import('/lib/shepherd/shepherd.mjs')
            .then(module => {
                window.Shepherd = module.default;
                launchTour();
            })
            .catch(e => console.error('[pivotTour] Failed to load Shepherd:', e));
    }

    return { start };
})();
