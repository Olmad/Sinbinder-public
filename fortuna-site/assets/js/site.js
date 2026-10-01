/* =====================================================================
   Фортуна — общий скрипт сайта
   Корзина и избранное (localStorage), меню каталога, поиск, страница
   товара, оформление заказа, формы обратной связи и чат.
   Заявки отправляются на send.php (нужен хостинг с PHP).
   ===================================================================== */
(function () {
    'use strict';

    var SEND_URL = 'send.php';
    var PHONE_HTML = '<a href="tel:88001012999">8 (800) 101-29-99</a>';
    var CART_KEY = 'fortuna_cart_v1';
    var FAV_KEY = 'fortuna_favorites_v1';
    var CHAT_CLOSED_KEY = 'fortuna_chat_closed';

    var products = window.FORTUNA_PRODUCTS || [];
    var categories = window.FORTUNA_CATEGORIES || [];
    var sitePages = window.FORTUNA_PAGES || [];

    var ICON_RUB = '<svg width="16px" height="19px" viewBox="0 0 17 20" xmlns="http://www.w3.org/2000/svg" class="catalog-products__item-price-currency"><path d="M9.647 15.091H6.056v3.09H4.008v-3.09H1.79v-1.776h2.22v-1.54h-2.22v-1.764h2.22V1.818h5.756c1.672 0 2.998.446 3.977 1.338.979.891 1.468 2.097 1.468 3.618 0 1.596-.471 2.829-1.415 3.698-.936.862-2.268 1.296-3.998 1.304H6.056v1.54h3.591v1.775zm-3.591-5.08h3.709c1.108 0 1.95-.273 2.53-.82.579-.547.868-1.345.868-2.394 0-.952-.3-1.72-.9-2.304-.6-.592-1.412-.892-2.434-.9H6.056v6.418z" fill="#010304"/></svg>';
    var ICON_HEART = '<svg width="18px" height="18px" viewBox="0 0 24 24" fill="none"><path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z" stroke="#13a5db" stroke-width="2" fill="none"/></svg>';
    var ICON_CART = '<svg width="18px" height="18px" viewBox="0 0 19 19" fill="none"><path d="M18.95 6.36v2.958a.74.74 0 01-.739.74h-.486l-.312 2.773a.74.74 0 01-1.47-.166l.386-3.43a.74.74 0 01.735-.656h.408v-1.48H1.498v1.48h12.609a.74.74 0 010 1.479H2.747l.73 6.092a1.48 1.48 0 001.468 1.303h9.138c.754 0 1.386-.565 1.47-1.314a.74.74 0 111.47.165 2.956 2.956 0 01-2.94 2.628H4.945a2.96 2.96 0 01-2.937-2.606l-.751-6.268H.758a.74.74 0 01-.74-.74V6.36a.74.74 0 01.74-.74h2.623L7.29.302a.74.74 0 111.192.875L5.216 5.62h8.564l-3.265-4.443a.74.74 0 111.193-.875l3.907 5.318h2.596a.74.74 0 01.74.74zM8.745 12.276v2.958a.74.74 0 101.48 0v-2.958a.74.74 0 00-1.48 0zm2.958 0v2.958a.74.74 0 101.48 0v-2.958a.74.74 0 00-1.48 0zm-5.916 0v2.958a.74.74 0 101.479 0v-2.958a.74.74 0 00-1.48 0z" fill="#13a5db" fill-rule="nonzero"/></svg>';

    // ------------------------------------------------------------ утилиты
    function $(sel, root) { return (root || document).querySelector(sel); }
    function $all(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }

    function esc(text) {
        return String(text == null ? '' : text)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function load(storage, key, fallback) {
        try {
            var raw = storage.getItem(key);
            return raw == null ? fallback : JSON.parse(raw);
        } catch (e) { return fallback; }
    }
    function save(storage, key, value) {
        try { storage.setItem(key, JSON.stringify(value)); } catch (e) { /* приватный режим — работаем без сохранения */ }
    }

    function formatPrice(value) {
        var parts = Number(value || 0).toFixed(2).split('.');
        parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
        return parts.join('.');
    }

    function findProduct(id) {
        for (var i = 0; i < products.length; i++) if (products[i].id === id) return products[i];
        return null;
    }
    function findCategory(id) {
        for (var i = 0; i < categories.length; i++) if (categories[i].id === id) return categories[i];
        return null;
    }
    function productUrl(p) { return 'product.html?id=' + encodeURIComponent(p.id); }

    function normalize(text) {
        return String(text || '').toLowerCase().replace(/ё/g, 'е').replace(/[^a-zа-я0-9]+/g, ' ').trim();
    }

    function getParam(name) {
        try { return new URLSearchParams(window.location.search).get(name) || ''; } catch (e) { return ''; }
    }

    // ------------------------------------------------------------ уведомление
    var toastTimer = null;
    function notify(html, timeout) {
        var toast = $('.site-toast');
        if (!toast) {
            toast = document.createElement('div');
            toast.className = 'site-toast';
            toast.setAttribute('role', 'status');
            toast.setAttribute('aria-live', 'polite');
            document.body.appendChild(toast);
        }
        toast.innerHTML = html;
        // перезапуск анимации
        toast.classList.remove('show');
        void toast.offsetWidth;
        toast.classList.add('show');
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { toast.classList.remove('show'); }, timeout || 3500);
    }

    // ------------------------------------------------------------ корзина
    var Cart = {
        items: function () {
            var items = load(localStorage, CART_KEY, []);
            if (!Array.isArray(items)) return [];
            return items.filter(function (it) { return it && findProduct(it.id) && it.qty > 0; });
        },
        save: function (items) {
            save(localStorage, CART_KEY, items);
            updateCartCount();
        },
        add: function (id, qty) {
            var items = Cart.items();
            var found = items.filter(function (it) { return it.id === id; })[0];
            if (found) found.qty = Math.min(99, found.qty + (qty || 1));
            else items.push({ id: id, qty: Math.min(99, qty || 1), size: '' });
            Cart.save(items);
        },
        update: function (id, patch) {
            var items = Cart.items();
            items.forEach(function (it) {
                if (it.id !== id) return;
                if (patch.qty != null) it.qty = Math.max(1, Math.min(99, parseInt(patch.qty, 10) || 1));
                if (patch.size != null) it.size = String(patch.size).slice(0, 100);
            });
            Cart.save(items);
        },
        remove: function (id) {
            Cart.save(Cart.items().filter(function (it) { return it.id !== id; }));
        },
        clear: function () { Cart.save([]); },
        count: function () {
            return Cart.items().reduce(function (sum, it) { return sum + it.qty; }, 0);
        },
        total: function () {
            return Cart.items().reduce(function (sum, it) { return sum + findProduct(it.id).price * it.qty; }, 0);
        }
    };
    window.FortunaCart = Cart;

    function updateCartCount() {
        var count = Cart.count();
        $all('[data-cart-count]').forEach(function (el) {
            el.textContent = count > 99 ? '99+' : String(count);
            el.hidden = count === 0;
        });
        var inCart = {};
        Cart.items().forEach(function (it) { inCart[it.id] = true; });
        $all('.catalog-products__item[data-id]').forEach(function (card) {
            var btn = $('.catalog-products__item-cart', card);
            if (btn) btn.classList.toggle('is-added', !!inCart[card.getAttribute('data-id')]);
        });
    }

    function addToCart(id, qty) {
        var p = findProduct(id);
        if (!p) return;
        Cart.add(id, qty || 1);
        notify('«' + esc(p.name) + '» добавлен в корзину.<a href="cart.html">Оформить заказ</a>');
    }

    // ------------------------------------------------------------ избранное
    function getFavorites() {
        var list = load(localStorage, FAV_KEY, []);
        return Array.isArray(list) ? list.filter(findProduct) : [];
    }
    function toggleFavorite(id) {
        var list = getFavorites();
        var p = findProduct(id);
        var index = list.indexOf(id);
        if (index === -1) {
            list.push(id);
            notify('«' + esc(p ? p.name : '') + '» добавлен в избранное.<a href="cart.html#favorites">Избранное</a>');
        } else {
            list.splice(index, 1);
            notify('Товар удалён из избранного.');
        }
        save(localStorage, FAV_KEY, list);
        updateFavoriteStates();
        if ($('#cartPage')) renderFavorites();
    }
    function updateFavoriteStates() {
        var list = getFavorites();
        $all('[data-id]').forEach(function (card) {
            var active = list.indexOf(card.getAttribute('data-id')) !== -1;
            $all('.catalog-products__item-favorite, [data-favorite]', card).forEach(function (btn) {
                btn.classList.toggle('is-active', active);
                btn.setAttribute('aria-pressed', active ? 'true' : 'false');
                if (btn.hasAttribute('data-favorite')) btn.textContent = active ? 'В избранном' : 'В избранное';
            });
        });
    }

    // ------------------------------------------------------------ карточка товара (как на страницах линеек)
    function productCardHTML(p) {
        var url = productUrl(p);
        return '<div class="catalog-products__item" data-id="' + esc(p.id) + '">' +
            '<a href="' + url + '" class="catalog-products__item-top"><img src="' + esc(p.image) + '" class="catalog-products__item-image" loading="lazy" alt="' + esc(p.name) + '"></a>' +
            '<a href="' + url + '" class="catalog-products__item-name">' + esc(p.name) + '</a>' +
            '<div class="catalog-products__item-row">' +
                '<div class="catalog-products__item-row-left"><div class="catalog-products__item-price">' +
                    '<span class="catalog-products__item-price-current">' + formatPrice(p.price) + '</span>' + ICON_RUB +
                '</div></div>' +
                '<div class="catalog-products__item-row-right">' +
                    '<div class="catalog-products__item-favorite" role="button" tabindex="0" title="В избранное" aria-label="В избранное" aria-pressed="false">' + ICON_HEART + '</div>' +
                    '<div class="catalog-products__item-cart" role="button" tabindex="0" title="В корзину" aria-label="В корзину">' + ICON_CART + '</div>' +
                '</div>' +
            '</div>' +
        '</div>';
    }
    function productGridHTML(list) {
        return '<div class="catalog-products__wr"><div class="catalog-products__items">' +
            list.map(productCardHTML).join('') + '</div></div>';
    }

    // ------------------------------------------------------------ клики (делегирование)
    document.addEventListener('click', function (e) {
        var target = e.target;
        if (!target.closest) return;

        var addBtn = target.closest('[data-add-to-cart]');
        if (addBtn) {
            e.preventDefault();
            var qtyInput = addBtn.getAttribute('data-qty-from') ? $(addBtn.getAttribute('data-qty-from')) : null;
            addToCart(addBtn.getAttribute('data-add-to-cart'), qtyInput ? parseInt(qtyInput.value, 10) || 1 : 1);
            return;
        }
        var cartIcon = target.closest('.catalog-products__item-cart');
        if (cartIcon) {
            var card = cartIcon.closest('[data-id]');
            if (card) addToCart(card.getAttribute('data-id'), 1);
            return;
        }
        var favBtn = target.closest('.catalog-products__item-favorite, [data-favorite]');
        if (favBtn) {
            var favCard = favBtn.closest('[data-id]');
            if (favCard) toggleFavorite(favCard.getAttribute('data-id'));
        }
    });

    // Enter / пробел для элементов-кнопок (иконки, стрелки слайдеров)
    document.addEventListener('keydown', function (e) {
        if ((e.key === 'Enter' || e.key === ' ') && e.target.getAttribute && e.target.getAttribute('role') === 'button' &&
            e.target.tagName !== 'BUTTON' && e.target.tagName !== 'A') {
            e.preventDefault();
            e.target.click();
        }
    });

    // синхронизация между вкладками
    window.addEventListener('storage', function (e) {
        if (e.key === CART_KEY) { updateCartCount(); if ($('#cartPage')) renderCart(); }
        if (e.key === FAV_KEY) updateFavoriteStates();
    });

    // ------------------------------------------------------------ меню каталога (бургер)
    function initCatalogMenu() {
        var burger = $('.header__burger');
        var menu = $('#catalogMenu');
        if (!burger || !menu) return;
        function setOpen(open) {
            menu.hidden = !open;
            burger.setAttribute('aria-expanded', open ? 'true' : 'false');
        }
        burger.addEventListener('click', function (e) {
            e.stopPropagation();
            setOpen(menu.hidden);
        });
        document.addEventListener('click', function (e) {
            if (!menu.hidden && !menu.contains(e.target)) setOpen(false);
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && !menu.hidden) { setOpen(false); burger.focus(); }
        });
    }

    // ------------------------------------------------------------ телефон
    function formatPhone(value) {
        var d = String(value).replace(/\D/g, '');
        if (!d) return '';
        if (d.charAt(0) === '8') d = '7' + d.slice(1);
        else if (d.charAt(0) !== '7') d = '7' + d;
        d = d.slice(0, 11);
        var out = '+7';
        if (d.length > 1) out += ' (' + d.slice(1, 4);
        if (d.length >= 4) out += ')';
        if (d.length > 4) out += ' ' + d.slice(4, 7);
        if (d.length > 7) out += '-' + d.slice(7, 9);
        if (d.length > 9) out += '-' + d.slice(9, 11);
        return out;
    }
    function isValidPhone(value) {
        return String(value).replace(/\D/g, '').length >= 11;
    }
    function attachPhoneMask(input) {
        if (input.dataset.masked) return;
        input.dataset.masked = '1';
        input.addEventListener('input', function (e) {
            if (e.inputType && e.inputType.indexOf('delete') === 0) return;
            input.value = formatPhone(input.value);
        });
    }

    // ------------------------------------------------------------ отправка
    function send(data) {
        if (window.location.protocol === 'file:') {
            return Promise.reject(new Error('file'));
        }
        return fetch(SEND_URL, {
            method: 'POST',
            body: data,
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            credentials: 'same-origin'
        }).then(function (response) {
            return response.json().catch(function () { return { ok: false }; }).then(function (json) {
                if (!response.ok || !json.ok) throw new Error(json.error || 'send');
                return json;
            });
        });
    }
    var SEND_ERROR = 'Не удалось отправить заявку. Пожалуйста, позвоните нам: ' + PHONE_HTML + '.';

    function setStatus(el, text, kind) {
        if (!el) return;
        el.className = 'form-status' + (kind ? ' is-' + kind : '');
        el.innerHTML = text;
    }

    function initForms() {
        $all('input[type="tel"]').forEach(attachPhoneMask);
        $all('form[data-form]').forEach(function (form) {
            if (form.dataset.bound) return;
            form.dataset.bound = '1';
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                var type = form.getAttribute('data-form');
                var status = form.querySelector('.form-status') ||
                    (form.nextElementSibling && form.nextElementSibling.classList.contains('form-status') ? form.nextElementSibling : null) ||
                    (form.parentNode.querySelector('.form-status'));

                var valid = true;
                $all('.is-invalid', form).forEach(function (el) { el.classList.remove('is-invalid'); });
                $all('[required]', form).forEach(function (el) {
                    var ok = el.type === 'checkbox' ? el.checked :
                        el.type === 'tel' ? isValidPhone(el.value) : el.value.trim() !== '';
                    if (!ok) { valid = false; el.classList.add('is-invalid'); }
                });
                if (!valid) {
                    var firstBad = $('.is-invalid', form);
                    if (firstBad) firstBad.focus();
                    setStatus(status, firstBad && firstBad.type === 'checkbox'
                        ? 'Нужно согласие на обработку персональных данных.'
                        : 'Проверьте, пожалуйста, заполнение полей (телефон — 11 цифр).', 'error');
                    return;
                }

                var data = new FormData(form);
                data.append('type', type);
                data.append('page', document.title + ' — ' + window.location.href);
                if (type === 'order') {
                    data.append('cart', JSON.stringify(Cart.items().map(function (it) {
                        var p = findProduct(it.id);
                        return { id: p.id, name: p.name, price: p.price, qty: it.qty, size: it.size || '' };
                    })));
                    data.append('total', Cart.total().toFixed(2));
                }

                var button = form.querySelector('[type="submit"]');
                if (button) button.disabled = true;
                setStatus(status, 'Отправляем…');

                send(data).then(function (result) {
                    if (type === 'order') {
                        Cart.clear();
                        showOrderSuccess(result && result.id);
                        return;
                    }
                    form.reset();
                    setStatus(status, 'Спасибо! Заявка принята — мы перезвоним вам в ближайшее время.', 'success');
                }).catch(function (err) {
                    setStatus(status, err && err.message === 'too_often'
                        ? 'Заявка уже отправлена. Если нужно отправить ещё одну — подождите несколько секунд.'
                        : SEND_ERROR, 'error');
                }).then(function () {
                    if (button) button.disabled = false;
                });
            });
        });
    }

    // ------------------------------------------------------------ чат
    function initChat() {
        var wrapper = $('#chatWrapper');
        var toggle = $('#chatToggleBtn');
        var close = $('#closeChatBtn');
        var form = $('#chatForm');
        var input = $('#chatInput');
        var message = $('#chatMessage');
        if (!wrapper || !toggle || !form) return;

        // на телефонах чат не открываем сам — он закрывал бы содержимое страницы
        if (window.matchMedia && window.matchMedia('(max-width: 640px)').matches) wrapper.classList.add('hidden');
        try {
            if (sessionStorage.getItem(CHAT_CLOSED_KEY)) wrapper.classList.add('hidden');
        } catch (e) { /* ignore */ }

        close.addEventListener('click', function () {
            wrapper.classList.add('hidden');
            try { sessionStorage.setItem(CHAT_CLOSED_KEY, '1'); } catch (e) { /* ignore */ }
        });
        toggle.addEventListener('click', function () {
            wrapper.classList.toggle('hidden');
            if (!wrapper.classList.contains('hidden') && input) input.focus();
        });

        var step = 'message';
        var text = '';
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            var value = input.value.trim();
            if (step === 'message') {
                if (!value) { input.focus(); return; }
                text = value;
                step = 'phone';
                message.innerHTML = 'Спасибо! Оставьте номер телефона — мы ответим вам в ближайшее время.' +
                    '<small class="chat-consent">Отправляя сообщение, вы соглашаетесь с <a href="privacy.html">политикой конфиденциальности</a>.</small>';
                input.value = '';
                input.type = 'tel';
                input.setAttribute('inputmode', 'tel');
                input.setAttribute('aria-label', 'Телефон');
                input.placeholder = '+7 (___) ___-__-__';
                attachPhoneMask(input);
                input.focus();
                return;
            }
            if (step === 'phone') {
                if (!isValidPhone(value)) {
                    input.classList.add('is-invalid');
                    input.focus();
                    return;
                }
                input.classList.remove('is-invalid');
                var data = new FormData();
                data.append('type', 'chat');
                data.append('message', text);
                data.append('phone', value);
                data.append('page', document.title + ' — ' + window.location.href);
                var button = form.querySelector('[type="submit"]');
                button.disabled = true;
                send(data).then(function () {
                    step = 'done';
                    form.hidden = true;
                    message.innerHTML = 'Сообщение отправлено! Мы свяжемся с вами в ближайшее время.';
                }).catch(function () {
                    message.innerHTML = 'Не удалось отправить сообщение. Позвоните нам: ' + PHONE_HTML + '.';
                }).then(function () { button.disabled = false; });
            }
        });
    }

    // ------------------------------------------------------------ страница товара
    function renderProductPage() {
        var root = $('#productPage');
        if (!root) return;
        var p = findProduct(getParam('id'));
        var title = $('#productTitle');
        var crumb = $('[data-crumb-current]');

        if (!p) {
            if (title) title.textContent = 'Товар не найден';
            if (crumb) crumb.textContent = 'Товар не найден';
            document.title = 'Товар не найден — Фортуна';
            root.innerHTML = '<div class="cart-empty"><p>Такого товара нет в каталоге.</p>' +
                '<a href="catalog.html" class="btn-primary">Перейти в каталог</a></div>';
            return;
        }
        var cat = findCategory(p.category);
        document.title = p.name + ' — Фортуна';
        var meta = $('meta[name="description"]');
        if (meta) meta.setAttribute('content', p.name + (cat ? ' — линейка «' + cat.name + '»' : '') +
            '. Цена от ' + formatPrice(p.price) + ' ₽. Производство «Фортуна», доставка, гарантия.');
        if (title) title.textContent = p.name;
        if (crumb) {
            crumb.textContent = p.name;
            if (cat) {
                var link = document.createElement('a');
                link.href = cat.url;
                link.className = 'breadcrumbs__item';
                link.textContent = cat.name;
                var sep = document.createElement('span');
                sep.className = 'breadcrumbs__separator';
                sep.textContent = '/';
                crumb.parentNode.insertBefore(link, crumb);
                crumb.parentNode.insertBefore(sep, crumb);
            }
        }

        var related = products.filter(function (x) { return x.category === p.category && x.id !== p.id; });
        root.innerHTML =
            '<div class="product" data-id="' + esc(p.id) + '">' +
                '<div class="product__gallery"><img src="' + esc(p.image) + '" alt="' + esc(p.name) + '"></div>' +
                '<div class="product__info">' +
                    (cat ? '<div class="product__category">Линейка: <a href="' + cat.url + '">' + esc(cat.name) + '</a></div>' : '') +
                    '<div class="product__price-label">Цена от</div>' +
                    '<div class="product__price">' + formatPrice(p.price) + ' ₽</div>' +
                    '<p class="product__note">Стоимость зависит от размера. Изготовим изделие по вашим размерам — укажите их в корзине, менеджер рассчитает точную цену.</p>' +
                    '<div class="product__actions">' +
                        '<div class="qty"><button type="button" data-qty-step="-1" aria-label="Меньше">−</button>' +
                        '<input type="number" id="productQty" min="1" max="99" value="1" aria-label="Количество">' +
                        '<button type="button" data-qty-step="1" aria-label="Больше">+</button></div>' +
                        '<button type="button" class="btn-primary" data-add-to-cart="' + esc(p.id) + '" data-qty-from="#productQty">В корзину</button>' +
                        '<button type="button" class="btn-outline" data-favorite aria-pressed="false">В избранное</button>' +
                    '</div>' +
                    '<ul class="product__benefits">' +
                        '<li>Собственное производство</li><li>Бесплатная доставка</li>' +
                        '<li>Гарантия 10 лет</li><li>Индивидуальные заказы</li>' +
                    '</ul>' +
                    '<div class="product-callback">' +
                        '<div class="product-callback__title">Есть вопросы по товару?</div>' +
                        '<div class="product-callback__text">Оставьте телефон — специалист перезвонит и поможет с выбором.</div>' +
                        '<form data-form="product" novalidate>' +
                            '<input type="hidden" name="product" value="' + esc(p.name) + '">' +
                            '<input type="text" name="website" class="form-hp" tabindex="-1" autocomplete="off" aria-hidden="true">' +
                            '<input type="tel" name="phone" placeholder="+7 (___) ___-__-__" required aria-label="Телефон">' +
                            '<button type="submit" class="btn-primary">Перезвоните мне</button>' +
                        '</form>' +
                        '<p class="form-consent">Нажимая кнопку, вы соглашаетесь с <a href="privacy.html">политикой конфиденциальности</a>.</p>' +
                        '<div class="form-status" role="status" aria-live="polite"></div>' +
                    '</div>' +
                '</div>' +
            '</div>' +
            (related.length ? '<section class="product-related"><h2 class="product-related__title">Другие модели линейки «' +
                esc(cat ? cat.name : '') + '»</h2>' + productGridHTML(related) + '</section>' : '');

        var qty = $('#productQty');
        $all('[data-qty-step]', root).forEach(function (btn) {
            btn.addEventListener('click', function () {
                var v = (parseInt(qty.value, 10) || 1) + parseInt(btn.getAttribute('data-qty-step'), 10);
                qty.value = Math.max(1, Math.min(99, v));
            });
        });
        qty.addEventListener('change', function () {
            qty.value = Math.max(1, Math.min(99, parseInt(qty.value, 10) || 1));
        });
    }

    // ------------------------------------------------------------ корзина и оформление
    function renderCart() {
        var root = $('#cartPage');
        if (!root) return;
        var items = Cart.items();
        if (!items.length) {
            root.innerHTML = '<div class="cart-empty"><p>Ваша корзина пуста.</p>' +
                '<a href="catalog.html" class="btn-primary">Перейти в каталог</a></div>' +
                '<div id="favorites" class="favorites"></div>';
            renderFavorites();
            return;
        }

        var listHTML = items.map(function (it) {
            var p = findProduct(it.id);
            var cat = findCategory(p.category);
            return '<div class="cart-item" data-cart-id="' + esc(p.id) + '">' +
                '<a class="cart-item__img" href="' + productUrl(p) + '"><img src="' + esc(p.image) + '" alt="' + esc(p.name) + '" loading="lazy"></a>' +
                '<div class="cart-item__info">' +
                    '<a class="cart-item__name" href="' + productUrl(p) + '">' + esc(p.name) + '</a>' +
                    '<div class="cart-item__cat">' + (cat ? 'Линейка «' + esc(cat.name) + '» · ' : '') + 'от ' + formatPrice(p.price) + ' ₽</div>' +
                    '<label class="cart-item__size"><input type="text" data-size maxlength="100" value="' + esc(it.size) + '" placeholder="Размер, например 160×200" aria-label="Размер"></label>' +
                '</div>' +
                '<div class="qty"><button type="button" data-step="-1" aria-label="Меньше">−</button>' +
                    '<input type="number" data-qty min="1" max="99" value="' + it.qty + '" aria-label="Количество">' +
                    '<button type="button" data-step="1" aria-label="Больше">+</button></div>' +
                '<div class="cart-item__sum">от ' + formatPrice(p.price * it.qty) + ' ₽</div>' +
                '<button type="button" class="cart-item__remove" data-remove aria-label="Удалить из корзины">×</button>' +
            '</div>';
        }).join('');

        // сохраняем то, что пользователь уже ввёл в форму, при перерисовке
        var prev = $('#orderForm');
        var draft = prev ? new FormData(prev) : null;

        root.innerHTML =
            '<div class="cart-layout">' +
                '<div>' +
                    '<div class="cart-list">' + listHTML + '</div>' +
                    '<div class="cart-total"><span>Итого:</span><strong>от ' + formatPrice(Cart.total()) + ' ₽</strong></div>' +
                    '<div class="cart-total__note">Цены указаны за минимальный размер. Точную стоимость с учётом размеров и доставки менеджер сообщит после оформления заказа.</div>' +
                '</div>' +
                '<form class="order-form" id="orderForm" data-form="order" novalidate>' +
                    '<div class="order-form__title">Оформление заказа</div>' +
                    '<input type="text" name="website" class="form-hp" tabindex="-1" autocomplete="off" aria-hidden="true">' +
                    '<div class="field"><label for="ofName">Имя *</label><input id="ofName" name="name" type="text" autocomplete="name" required maxlength="100"></div>' +
                    '<div class="field"><label for="ofPhone">Телефон *</label><input id="ofPhone" name="phone" type="tel" autocomplete="tel" placeholder="+7 (___) ___-__-__" required></div>' +
                    '<div class="field"><label for="ofEmail">E-mail</label><input id="ofEmail" name="email" type="email" autocomplete="email" maxlength="100"></div>' +
                    '<div class="field"><span class="field__label">Получение</span><div class="choice">' +
                        '<label><input type="radio" name="delivery" value="Доставка" checked> Доставка</label>' +
                        '<label><input type="radio" name="delivery" value="Самовывоз"> Самовывоз</label>' +
                    '</div></div>' +
                    '<div class="field"><label for="ofAddress">Город и адрес доставки</label><input id="ofAddress" name="address" type="text" autocomplete="street-address" maxlength="300"></div>' +
                    '<div class="field"><span class="field__label">Способ оплаты</span><div class="choice">' +
                        '<label><input type="radio" name="payment" value="Наличными или картой при получении" checked> Наличными или картой при получении</label>' +
                        '<label><input type="radio" name="payment" value="Банковский перевод по счёту"> Банковский перевод по счёту</label>' +
                    '</div></div>' +
                    '<div class="field"><label for="ofComment">Комментарий</label><textarea id="ofComment" name="comment" maxlength="2000"></textarea></div>' +
                    '<label class="consent"><input type="checkbox" name="consent" value="да" required> <span>Я согласен на обработку персональных данных в соответствии с <a href="privacy.html" target="_blank">политикой конфиденциальности</a></span></label>' +
                    '<button type="submit" class="btn-primary">Оформить заказ</button>' +
                    '<div class="form-status" role="status" aria-live="polite"></div>' +
                '</form>' +
            '</div>' +
            '<div id="favorites" class="favorites"></div>';

        if (draft) {
            var form = $('#orderForm');
            draft.forEach(function (value, key) {
                var field = form.elements[key];
                if (!field || key === 'website') return;
                if (field.length && field[0] && field[0].type === 'radio') {
                    $all('input[name="' + key + '"]', form).forEach(function (r) { r.checked = r.value === value; });
                } else if (field.type === 'checkbox') {
                    field.checked = true;
                } else {
                    field.value = value;
                }
            });
        }

        $all('.cart-item', root).forEach(function (row) {
            var id = row.getAttribute('data-cart-id');
            var qty = $('[data-qty]', row);
            $all('[data-step]', row).forEach(function (btn) {
                btn.addEventListener('click', function () {
                    Cart.update(id, { qty: (parseInt(qty.value, 10) || 1) + parseInt(btn.getAttribute('data-step'), 10) });
                    renderCart();
                });
            });
            qty.addEventListener('change', function () { Cart.update(id, { qty: qty.value }); renderCart(); });
            $('[data-size]', row).addEventListener('input', function (e) { Cart.update(id, { size: e.target.value }); });
            $('[data-remove]', row).addEventListener('click', function () { Cart.remove(id); renderCart(); });
        });
        initForms();
        renderFavorites();
    }

    function renderFavorites() {
        var box = $('#favorites');
        if (!box) return;
        var list = getFavorites().map(findProduct);
        box.innerHTML = list.length
            ? '<h2 class="product-related__title">Избранное</h2>' + productGridHTML(list)
            : '';
        updateFavoriteStates();
        updateCartCount();
    }

    function showOrderSuccess(orderId) {
        var root = $('#cartPage');
        if (!root) return;
        root.innerHTML = '<div class="cart-success">' +
            '<p class="cart-success__title">Спасибо! Заказ ' + (orderId ? '№ ' + esc(orderId) + ' ' : '') + 'принят.</p>' +
            '<p>Менеджер свяжется с вами, чтобы уточнить размеры, сроки изготовления и доставку.</p>' +
            '<a href="catalog.html" class="btn-primary">Вернуться в каталог</a></div>';
        window.scrollTo(0, 0);
    }

    // ------------------------------------------------------------ поиск
    function renderSearch() {
        var root = $('#searchResults');
        if (!root) return;
        var query = getParam('query').trim();
        $all('input[name="query"]').forEach(function (input) { input.value = query; });
        if (query) document.title = 'Поиск: ' + query + ' — Фортуна';

        var words = normalize(query).split(' ').filter(Boolean);
        if (!words.length) {
            root.innerHTML = '<p class="search-page__summary">Введите название товара или линейки, например «Кроха», «латекс» или «топпер».</p>';
            return;
        }
        function matches(text) {
            var hay = normalize(text);
            return words.every(function (w) { return hay.indexOf(w) !== -1; });
        }
        var found = products.filter(function (p) {
            var cat = findCategory(p.category);
            return matches(p.name + ' ' + (cat ? cat.name : ''));
        });
        var foundPages = sitePages.filter(function (pg) { return matches(pg.title + ' ' + pg.keywords); });
        var foundCats = categories.filter(function (c) { return matches(c.name); });

        var html = '';
        var links = foundCats.map(function (c) { return { title: 'Линейка «' + c.name + '»', url: c.url }; })
            .concat(foundPages.map(function (pg) { return { title: pg.title, url: pg.url }; }));
        if (links.length) {
            html += '<ul class="search-page__pages">' + links.map(function (l) {
                return '<li><a href="' + l.url + '">' + esc(l.title) + '</a></li>';
            }).join('') + '</ul>';
        }
        if (found.length) {
            html += '<p class="search-page__summary">Найдено товаров: ' + found.length + '</p>' + productGridHTML(found);
        } else if (!links.length) {
            html += '<p class="search-page__summary">По запросу «' + esc(query) + '» ничего не найдено. ' +
                'Попробуйте изменить запрос или посмотрите <a href="catalog.html">каталог</a>.</p>';
        }
        root.innerHTML = html;
    }

    // ------------------------------------------------------------ старт
    function init() {
        $all('[data-year]').forEach(function (el) { el.textContent = new Date().getFullYear(); });
        initCatalogMenu();
        initChat();
        renderProductPage();
        renderCart();
        renderSearch();
        initForms();
        updateCartCount();
        updateFavoriteStates();
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
