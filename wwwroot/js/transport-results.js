(() => {
    const fareText = fare => !fare ? 'Fare unavailable' : `${fare.estimated ? 'Estimated ' : ''}BDT ${fare.minimumBdt}${fare.maximumBdt !== fare.minimumBdt ? `–${fare.maximumBdt}` : ''}`;
    function mount({ document = globalThis.document, form = globalThis.TransportForm?.current } = {}) {
        const options = document.getElementById('transport-options'); if (!options) return;
        const element = (tag, text) => { const node = document.createElement(tag); node.textContent = text; return node; };
        function detail(option) {
            const container = document.getElementById('transport-legs'); container.replaceChildren();
            document.getElementById('transport-detail').hidden = false;
            container.appendChild(element('p', `${option.minutes} min including 5 min per transfer · ${option.distanceKm} km · ${option.transfers} transfers · ${fareText(option.fare)}`));
            const list = document.createElement('ol');
            for (const leg of option.legs) {
                const item = element('li', leg.direction); item.className = 'transport-leg';
                item.appendChild(element('p', `${leg.minutes} min · ${Number(leg.distanceKm).toFixed(2)} km · ${fareText(leg.fare)}`));
                item.appendChild(element('p', `${leg.isSample ? 'Demonstration data. ' : ''}Route source: ${leg.source || 'Unspecified'}. Fare source: ${leg.fare?.source || 'Unavailable'}.`)); list.appendChild(item);
            }
            container.appendChild(list); form?.draw(option);
        }
        function render(result) {
            options.replaceChildren(); document.getElementById('transport-detail').hidden = true;
            if (!result.options.length) { options.appendChild(element('p', 'No routes found for these locations')); return; }
            for (const option of result.options) {
                const card = element('article', ''); card.className = 'transport-option';
                card.appendChild(element('h3', [...new Set(option.legs.map(leg => leg.mode))].join(' → ')));
                card.appendChild(element('p', `${option.minutes} min · ${option.distanceKm} km · ${option.transfers} transfers`));
                card.appendChild(element('p', fareText(option.fare)));
                if (option.isSample) card.appendChild(element('p', 'Demonstration route — not operator verified.'));
                const show = element('button', 'Show steps on this page'); show.type = 'button'; show.addEventListener('click', () => detail(option)); card.appendChild(show);
                const query = new URLSearchParams(result.query); query.set('id', option.id); query.set('originName', result.origin.name); query.set('destinationName', result.destination.name);
                const link = element('a', 'Open route detail page'); link.href = `/transport/route?${query}`; card.appendChild(link); options.appendChild(card);
            }
            const selected = result.options.find(option => option.id === result.selectedId);
            if (selected) detail(selected);
            else if (result.selectedId) options.prepend(element('p', 'That selected route is no longer available. Choose one of the current options.'));
        }
        document.addEventListener('transport-results', event => render(event.detail));
        if (form?.getLatest()) render(form.getLatest());
        return { render, detail };
    }
    globalThis.TransportResults = { mount, fareText };
    if (typeof module !== 'undefined') module.exports = globalThis.TransportResults;
    if (typeof document !== 'undefined') mount();
})();
