document.getElementById('filterForm').addEventListener('submit', async function(e) {
    e.preventDefault();
    const checked = Array.from(document.querySelectorAll('#subjects-checkboxes input[type=checkbox]:checked'));
    const subjects = checked.map(cb => {
        const subject = cb.value;
        const scoreInput = document.querySelector(`.score-input[data-subject="${subject}"]`);
        const score = scoreInput && scoreInput.value ? parseInt(scoreInput.value) : null;
        return { subject, score };
    });
    const area = document.getElementById('area').value.trim() || null;
    const form = document.getElementById('form').value || null;

    const body = {
        subjects,
        area,
        form: form === '' ? null : form
    };

    const resultsDiv = document.getElementById('results');
    resultsDiv.innerHTML = '<p>Загрузка...</p>';

    try {
        const response = await fetch('/api/directions/filter', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        if (!response.ok) throw new Error('Ошибка запроса');
        const data = await response.json();
        if (data.length === 0) {
            resultsDiv.innerHTML = '<p>Нет подходящих направлений.</p>';
            return;
        }
        resultsDiv.innerHTML = data.map(item => `
            <div class="result-item">
                <h3>${item.name} (${item.code})</h3>
                <p><b>Форма обучения:</b> ${item.form}</p>
                <p><b>Мест:</b> ${item.places ?? '-'}</p>
                <p><b>Проходной балл (бюджет):</b> ${item.scoreBudget ?? '-'}</p>
                <p><b>Проходной балл (платное):</b> ${item.scorePaid ?? '-'}</p>
                <p><b>Стоимость (год):</b> ${item.cost ?? '-'}</p>
                <p><b>Предметы ЕГЭ:</b> ${item.subjects.join(', ')}</p>
                ${item.passType ? `<p style="color:green"><b>Вы проходите на: ${item.passType}</b></p>` : ''}
            </div>
        `).join('');
    } catch (err) {
        resultsDiv.innerHTML = '<p style="color:red">Ошибка при получении данных.</p>';
    }
}); 