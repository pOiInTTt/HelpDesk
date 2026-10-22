async function loadDictionaries() {
  try {
    const [catRes, priRes] = await Promise.all([
      fetch('/api/categories'),
      fetch('/api/priorities')
    ]);

    if (!catRes.ok || !priRes.ok) throw new Error('Не удалось загрузить справочники');

    const categories = await catRes.json();
    const priorities = await priRes.json();

    const catSelect = document.getElementById('category');
    catSelect.innerHTML = categories
      .map(c => `<option value="${c.id}">${c.name}</option>`)
      .join('');

    const priSelect = document.getElementById('priority');
    priSelect.innerHTML = priorities
      .map(p => `<option value="${p.id}">${p.name}</option>`)
      .join('');
  } catch (e) {
    document.getElementById('result').textContent =
      'Не удалось загрузить категории и приоритеты. Обновите страницу позже.';
  }
}

async function sendRequest() {
  const resultEl = document.getElementById('result');
  resultEl.textContent = '';

  const name = document.getElementById('name').value.trim();
  const email = document.getElementById('email').value.trim();
  const phone = document.getElementById('phone').value.trim();
  const title = document.getElementById('title').value.trim();
  const desc = document.getElementById('desc').value.trim();
  const categoryId = parseInt(document.getElementById('category').value, 10);
  const priorityId = parseInt(document.getElementById('priority').value, 10);

  if (!name) return (resultEl.textContent = 'Укажите имя.');
  if (!email) return (resultEl.textContent = 'Укажите почту.');
  if (!title) return (resultEl.textContent = 'Укажите тему заявки.');
  if (!categoryId) return (resultEl.textContent = 'Выберите категорию.');
  if (!priorityId) return (resultEl.textContent = 'Выберите приоритет.');

  const body = {
    clientName: name,
    clientEmail: email,
    clientPhone: phone || null,
    title,
    description: desc || null,
    categoryId,
    priorityId
  };

  try {
    const res = await fetch('/api/requests', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });

    if (!res.ok) {
      let msg = 'Не удалось отправить заявку.';
      try {
        const err = await res.json();
        if (err.error) msg = err.error;
        else if (err.errors) msg = Object.values(err.errors).flat().join('; ');
      } catch {}
      resultEl.textContent = msg;
      return;
    }

    const data = await res.json();
    resultEl.textContent = `Заявка создана. Номер: ${data.id}`;
  } catch (e) {
    resultEl.textContent = 'Сервер недоступен. Попробуйте позже.';
  }
}

async function checkStatus() {
  const statusEl = document.getElementById('status');
  statusEl.textContent = '';

  const id = parseInt(document.getElementById('num').value, 10);
  if (!id || id < 1) {
    statusEl.textContent = 'Укажите корректный номер заявки.';
    return;
  }

  try {
    const res = await fetch(`/api/requests/${id}`);

    if (res.status === 404) {
      statusEl.textContent = `Заявка №${id} не найдена.`;
      return;
    }
    if (!res.ok) {
      statusEl.textContent = 'Не удалось получить статус. Попробуйте позже.';
      return;
    }

    const data = await res.json();
    const employee = data.employeeName ? data.employeeName : 'не назначен';
    statusEl.textContent = `Статус: ${data.status}. Исполнитель: ${employee}.`;
  } catch (e) {
    statusEl.textContent = 'Сервер недоступен. Попробуйте позже.';
  }
}

document.addEventListener('DOMContentLoaded', () => {
  document.getElementById('send').addEventListener('click', sendRequest);
  document.getElementById('btn').addEventListener('click', checkStatus);
  loadDictionaries();
});


async function assignEmployee() {
  const resultEl = document.getElementById('emp-result');
  resultEl.textContent = '';

  const id = parseInt(document.getElementById('emp-req-id').value, 10);
  const employeeId = parseInt(document.getElementById('emp-id').value, 10);

  if (!id) return (resultEl.textContent = 'Укажите номер заявки.');
  if (!employeeId) return (resultEl.textContent = 'Укажите ID сотрудника.');

  try {
    const res = await fetch(`/api/requests/${id}/assignee`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ employeeId })
    });

    if (!res.ok) {
      const err = await res.json();
      resultEl.textContent = err.error || 'Не удалось назначить исполнителя.';
      return;
    }

    const data = await res.json();
    resultEl.textContent = `Успешно! Исполнитель назначен. Новый статус: ${data.status}`;
  } catch (e) {
    resultEl.textContent = 'Сервер недоступен. Попробуйте позже.';
  }
}

async function changeStatus() {
  const resultEl = document.getElementById('emp-result');
  resultEl.textContent = '';

  const id = parseInt(document.getElementById('emp-req-id').value, 10);
  const status = document.getElementById('new-status').value;

  if (!id) return (resultEl.textContent = 'Укажите номер заявки.');

  try {
    const res = await fetch(`/api/requests/${id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status })
    });

    if (!res.ok) {
      const err = await res.json();
      resultEl.textContent = err.error || 'Не удалось изменить статус.';
      return;
    }

    const data = await res.json();
    resultEl.textContent = `Успешно! Новый статус: ${data.status}`;
  } catch (e) {
    resultEl.textContent = 'Сервер недоступен. Попробуйте позже.';
  }
}

document.addEventListener('DOMContentLoaded', () => {
  document.getElementById('send').addEventListener('click', sendRequest);
  document.getElementById('btn').addEventListener('click', checkStatus);
  document.getElementById('btn-assign').addEventListener('click', assignEmployee);
  document.getElementById('btn-status').addEventListener('click', changeStatus);
  
  loadDictionaries();
});