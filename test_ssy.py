from flask import Flask, request, jsonify
from flask_cors import CORS
import math
import re
import json  # 用于格式化JSON输出

app = Flask(__name__)
CORS(app)


def safe_eval(expression, angle_unit='deg'):
    """安全计算表达式（原有逻辑不变）"""
    if not re.match(r'^[0-9+\-*/().πe%√!^sincotanlg\s]+$', expression):
        return None, "非法字符（仅支持数字、运算符和数学函数）"

    expr = (expression.replace('π', 'math.pi')
            .replace('e', 'math.e')
            .replace('√', 'math.sqrt')
            .replace('^', '**')
            .replace('x²', '**2')
            .replace('x³', '**3'))

    if angle_unit == 'deg':
        expr = re.sub(r'\b(sin|cos|tan)\(([^)]+)\)', r'\1(math.radians(\2))', expr)

    expr = re.sub(
        # 匹配两种模式：
        # 1. !(8) → 感叹号+左括号+数字+右括号
        # 2. 8!   → 数字+感叹号
        r'!\((\d+)\)|\b(\d+)\!',
        lambda m: str(math.factorial(int(m.group(1) or m.group(2)))) if int(m.group(1) or m.group(2)) >= 0 else '0',
        expr
    )
    expr = expr.replace('log', 'math.log10').replace('ln', 'math.log')


    try:
        result = eval(expr, {'__builtins__': {}}, {'math': math})
        return f"{result:.10f}".rstrip('0').rstrip('.') if isinstance(result, float) else str(result), None
    except Exception as e:
        return None, f"计算错误: {str(e)}"


@app.route('/calculate', methods=['POST'])
def calculate():
    # 1. 打印前端发送的请求JS串
    raw_request = request.get_json()
    print("\n===== 接收到的前端请求JS串 =====")
    print(json.dumps(raw_request, indent=2, ensure_ascii=False))

    # 2. 处理请求
    if not raw_request or 'expression' not in raw_request:
        # 构造错误响应并打印
        error_response = {'error': '缺少必要参数：expression'}
        print("\n===== 返回给前端的响应JS串 =====")
        print(json.dumps(error_response, indent=2, ensure_ascii=False))
        return jsonify(error_response), 400

    expression = raw_request['expression'].strip()
    angle_unit = raw_request.get('unit', 'deg')

    result, error = safe_eval(expression, angle_unit)
    if error:
        # 构造错误响应并打印
        error_response = {'error': error}
        print("\n===== 返回给前端的响应JS串 =====")
        print(json.dumps(error_response, indent=2, ensure_ascii=False))
        return jsonify(error_response), 400

    # 3. 构造成功响应并打印
    success_response = {
        'result': result,
        'unit': angle_unit
    }
    print("\n===== 返回给前端的响应JS串 =====")
    print(json.dumps(success_response, indent=2, ensure_ascii=False))

    return jsonify(success_response)


if __name__ == '__main__':
    app.run(host='0.0.0.0', port=8000, debug=True)