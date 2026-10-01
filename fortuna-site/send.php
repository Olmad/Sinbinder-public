<?php
/**
 * Фортуна — обработчик заявок с сайта.
 * Принимает формы: заказ звонка, вопрос по товару, сообщение из чата, заказ из корзины.
 * Отправляет заявку на e-mail и (по желанию) в Telegram.
 *
 * Требуется хостинг с PHP 7.0+.
 */

// ======================= НАСТРОЙКИ =======================

// Куда приходят заявки (можно несколько через запятую)
$TO_EMAIL = 'Fortyna2006@mail.ru';

// Адрес отправителя. Лучше указать ящик на домене сайта (например, info@ваш-домен.ru),
// иначе письма чаще попадают в спам.
$FROM_EMAIL = 'no-reply@' . preg_replace('/^www\./', '', preg_replace('/:\d+$/', '', $_SERVER['HTTP_HOST'] ?? 'localhost'));

// Telegram (необязательно): токен бота от @BotFather и ID чата, куда слать заявки.
$TELEGRAM_BOT_TOKEN = '';
$TELEGRAM_CHAT_ID = '';

// Резервная запись заявок в файл (необязательно). Указывайте путь ВНЕ папки сайта,
// например: __DIR__ . '/../fortuna-orders.log'. Пустая строка — не записывать.
$LOG_FILE = '';

// =========================================================

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

function respond($ok, $extra = array(), $code = 200)
{
    http_response_code($code);
    echo json_encode(array_merge(array('ok' => $ok), $extra), JSON_UNESCAPED_UNICODE);
    exit;
}

function field($name, $max = 500)
{
    $value = isset($_POST[$name]) ? (string)$_POST[$name] : '';
    $value = strip_tags($value);
    $value = preg_replace('/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/u', '', $value);
    $value = trim($value);
    if (function_exists('mb_substr')) {
        return mb_substr($value, 0, $max, 'UTF-8');
    }
    return substr($value, 0, $max);
}

if (($_SERVER['REQUEST_METHOD'] ?? 'GET') !== 'POST') {
    respond(false, array('error' => 'method'), 405);
}

// Защита от ботов: скрытое поле должно остаться пустым
if (field('website') !== '') {
    respond(true);
}

$types = array(
    'callback' => 'Заказ звонка',
    'product'  => 'Вопрос по товару',
    'chat'     => 'Сообщение из чата на сайте',
    'order'    => 'Новый заказ',
);
$type = field('type', 20);
if (!isset($types[$type])) {
    respond(false, array('error' => 'type'), 400);
}

// Защита от повторной отправки: одна заявка каждого типа раз в 10 секунд с одного браузера
if (session_status() === PHP_SESSION_NONE) {
    @session_start();
}
$rateKey = 'fortuna_last_send_' . $type;
if (isset($_SESSION[$rateKey]) && time() - $_SESSION[$rateKey] < 10) {
    respond(false, array('error' => 'too_often'), 429);
}

$phone = field('phone', 30);
if (strlen(preg_replace('/\D/', '', $phone)) < 10) {
    respond(false, array('error' => 'phone'), 422);
}

$email = field('email', 100);
if ($email !== '' && !filter_var($email, FILTER_VALIDATE_EMAIL)) {
    $email = '';
}

$orderId = date('dmy') . '-' . str_pad((string)mt_rand(0, 9999), 4, '0', STR_PAD_LEFT);
$subject = $types[$type] . ($type === 'order' ? ' № ' . $orderId : '') . ' — сайт «Фортуна»';

$lines = array();
$lines[] = $subject;
$lines[] = str_repeat('-', 40);
$name = field('name', 100);
if ($name !== '') $lines[] = 'Имя: ' . $name;
$lines[] = 'Телефон: ' . $phone;
if ($email !== '') $lines[] = 'E-mail: ' . $email;

if ($type === 'product') {
    $lines[] = 'Товар: ' . field('product', 200);
}
if ($type === 'chat') {
    $lines[] = 'Сообщение: ' . field('message', 2000);
}
if ($type === 'order') {
    $lines[] = 'Получение: ' . field('delivery', 50);
    $address = field('address', 300);
    if ($address !== '') $lines[] = 'Адрес: ' . $address;
    $lines[] = 'Оплата: ' . field('payment', 100);
    $comment = field('comment', 2000);
    if ($comment !== '') $lines[] = 'Комментарий: ' . $comment;
    $lines[] = '';
    $lines[] = 'Состав заказа:';
    $cart = json_decode(isset($_POST['cart']) ? (string)$_POST['cart'] : '[]', true);
    if (!is_array($cart) || !count($cart)) {
        respond(false, array('error' => 'empty_cart'), 422);
    }
    $n = 0;
    foreach (array_slice($cart, 0, 50) as $item) {
        if (!is_array($item)) continue;
        $n++;
        $itemName = strip_tags((string)($item['name'] ?? ''));
        $qty = max(1, min(99, (int)($item['qty'] ?? 1)));
        $price = (float)($item['price'] ?? 0);
        $size = trim(strip_tags((string)($item['size'] ?? '')));
        $lines[] = $n . '. ' . $itemName . ' — ' . $qty . ' шт., цена от ' . number_format($price, 2, '.', ' ') . ' ₽'
            . ($size !== '' ? ', размер: ' . $size : ', размер не указан');
    }
    $lines[] = 'Итого: от ' . number_format((float)field('total', 20), 2, '.', ' ') . ' ₽';
    $lines[] = 'Согласие на обработку ПДн: ' . (field('consent', 5) === 'да' ? 'да' : 'нет');
}
$lines[] = '';
$lines[] = 'Страница: ' . field('page', 300);
$lines[] = 'Дата: ' . date('d.m.Y H:i');
$lines[] = 'IP: ' . ($_SERVER['REMOTE_ADDR'] ?? '');
$text = implode("\n", $lines);

$delivered = false;

// --- e-mail
if ($TO_EMAIL !== '') {
    $headers = array(
        'From: =?UTF-8?B?' . base64_encode('Сайт Фортуна') . '?= <' . $FROM_EMAIL . '>',
        'MIME-Version: 1.0',
        'Content-Type: text/plain; charset=UTF-8',
        'Content-Transfer-Encoding: base64',
    );
    if ($email !== '') {
        $headers[] = 'Reply-To: ' . $email;
    }
    $delivered = @mail(
        $TO_EMAIL,
        '=?UTF-8?B?' . base64_encode($subject) . '?=',
        chunk_split(base64_encode($text)),
        implode("\r\n", $headers)
    ) || $delivered;
}

// --- Telegram
if ($TELEGRAM_BOT_TOKEN !== '' && $TELEGRAM_CHAT_ID !== '') {
    $payload = http_build_query(array('chat_id' => $TELEGRAM_CHAT_ID, 'text' => $text));
    $context = stream_context_create(array('http' => array(
        'method'  => 'POST',
        'header'  => "Content-Type: application/x-www-form-urlencoded\r\n",
        'content' => $payload,
        'timeout' => 10,
    )));
    $result = @file_get_contents('https://api.telegram.org/bot' . $TELEGRAM_BOT_TOKEN . '/sendMessage', false, $context);
    $delivered = ($result !== false && strpos($result, '"ok":true') !== false) || $delivered;
}

// --- резервный файл
if ($LOG_FILE !== '') {
    $saved = @file_put_contents($LOG_FILE, $text . "\n\n" . str_repeat('=', 40) . "\n\n", FILE_APPEND | LOCK_EX);
    $delivered = ($saved !== false) || $delivered;
}

if (!$delivered) {
    respond(false, array('error' => 'delivery'), 500);
}

$_SESSION[$rateKey] = time();
respond(true, array('id' => $orderId));
