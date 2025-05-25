from flask import Flask, request, jsonify
from queue import Queue, Empty
from threading import Thread, Lock
import requests
import time
import uuid
from datetime import datetime, timedelta

# 配置项
BACKEND_URL = "https://10.244.121.103:44310/calculator/calculate"
MAX_QUEUE_SIZE = 100
REQUEST_TIMEOUT = 15
RESULT_EXPIRE_SECONDS = 300  # 结果缓存5分钟过期

app = Flask(__name__)
request_queue = Queue(maxsize=MAX_QUEUE_SIZE)
processing = False
result_cache = {}  # 存储结果：{request_id: (result, timestamp)}
cache_lock = Lock()  # 缓存访问锁


# 日志工具
def log_request(request_id: str, action: str, details: str = ""):
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    app.logger.info(f"[{timestamp}] [RequestID: {request_id}] {action} | {details}")


# 清理过期缓存（每小时执行一次）
def clean_expired_cache():
    while True:
        time.sleep(3600)  # 每小时清理一次
        with cache_lock:
            now = datetime.now()
            expired_ids = [
                rid for rid, (_, ts) in result_cache.items()
                if (now - ts) > timedelta(seconds=RESULT_EXPIRE_SECONDS)
            ]
            for rid in expired_ids:
                del result_cache[rid]
            app.logger.info(f"清理过期结果缓存，共 {len(expired_ids)} 条")


# 启动缓存清理线程
Thread(target=clean_expired_cache, daemon=True).start()


# 后台队列处理线程（修改：存储结果到缓存）
def process_queue(app_instance):
    global processing
    while True:
        if not processing and not request_queue.empty():
            processing = True
            try:
                req_data = request_queue.get(timeout=3)
                request_id = req_data["request_id"]
                log_request(request_id, "开始处理请求", f"表达式: {req_data['expression']}")

                # ---------------------- 新增：打印向后端发送的请求 ----------------------
                backend_request_data = {
                    "Expression": req_data["expression"],
                    "Id": request_id,
                    "unit": req_data.get("unit", "deg")
                }
                log_request(request_id, "向后端发送请求", f"请求体: {backend_request_data}")  # 打印请求内容
                # ----------------------------------------------------------------------

                # 转发请求到后端
                backend_response = requests.post(
                    BACKEND_URL,
                    json=backend_request_data,  # 使用变量传递请求体
                    timeout=REQUEST_TIMEOUT,
                    verify = False
                )
                backend_response.raise_for_status()

                # 解析后端结果
                backend_data = backend_response.json()
                result = {
                    "success": True,
                    "data": backend_data["Result"],
                    # "unit": backend_data.get("unit", ""),
                    "request_id": backend_data["Id"],
                    "timestamp": datetime.now().isoformat()
                }
                log_request(request_id, "后端处理成功", f"结果: {result['data']}")

            except requests.exceptions.RequestException as e:
                result = {
                    "success": False,
                    "error": f"后端请求失败: {str(e)}",
                    "request_id": request_id,
                    "timestamp": datetime.now().isoformat()
                }
                log_request(request_id, "后端请求失败", str(e))

            except Empty:
                result = {"success": False, "error": "请求队列已空"}
                processing = False
                continue

            except Exception as e:
                result = {
                    "success": False,
                    "error": f"中间层处理异常: {str(e)}",
                    "request_id": request_id,
                    "timestamp": datetime.now().isoformat()
                }
                log_request(request_id, "中间层处理异常", str(e))

            finally:
                # 存储结果到缓存（加锁保证线程安全）
                with cache_lock:
                    result_cache[request_id] = (result, datetime.now())

                # 执行回调（保持原有逻辑）
                def response_callback(result, app_instance):
                    with app_instance.app_context():
                        return jsonify(result)

                response = req_data["response_callback"](result, app_instance)
                request_queue.task_done()
                processing = False

        time.sleep(0.1)


# 启动队列处理线程
Thread(target=process_queue, args=(app,), daemon=True).start()


# 中间层核心接口（接收前端请求）
@app.route("/calculator/calculate", methods=["POST"])
def handle_calculate():
    try:
        req_data = request.json
        if not req_data:
            return jsonify({"success": False, "error": "请求体为空"}), 400

        if "expression" not in req_data:
            return jsonify({"success": False, "error": "缺少必要参数: expression"}), 400
        if not isinstance(req_data["expression"], str):
            return jsonify({"success": False, "error": "expression必须为字符串"}), 400

        request_id = str(uuid.uuid4())
        log_request(request_id, "接收新请求", f"表达式: {req_data['expression']}")

        if request_queue.full():
            log_request(request_id, "队列已满", f"当前队列长度: {request_queue.qsize()}")
            return jsonify({
                "success": False,
                "error": "系统繁忙，请稍后再试",
                "request_id": request_id
            }), 503

        def response_callback(result, app_instance):
            with app_instance.app_context():
                return jsonify(result)

        request_queue.put({
            "request_id": request_id,
            "expression": req_data["expression"],
            "unit": req_data.get("unit", "deg"),
            "response_callback": response_callback,
            "timestamp": datetime.now().isoformat()
        })

        return jsonify({
            "success": True,
            "message": "请求已加入处理队列",
            "request_id": request_id,
            "queue_position": request_queue.qsize(),
            "estimated_wait": request_queue.qsize() * 2
        }), 202

    except Exception as e:
        log_request("N/A", "接口异常", str(e))
        return jsonify({
            "success": False,
            "error": "服务器内部错误",
            "request_id": "N/A"
        }), 500


# 新增：轮询结果接口
@app.route("/calculator/result/<request_id>", methods=["GET"])
def get_result(request_id):
    with cache_lock:
        cached = result_cache.get(request_id)
        if not cached:
            return jsonify({
                "success": False,
                "status": "pending",
                "message": "结果尚未处理完成"
            })

        result, _ = cached
        del result_cache[request_id]  # 读取后删除缓存（避免重复查询）
        return jsonify({
            "success": True,
            "status": "completed",
            "result": result
        })


# 跨域支持（保持原有逻辑）
@app.after_request
def add_cors_headers(response):
    response.headers["Access-Control-Allow-Origin"] = "*"
    response.headers["Access-Control-Allow-Methods"] = "POST, GET"
    response.headers["Access-Control-Allow-Headers"] = "Content-Type"
    return response


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=8000, debug=True)