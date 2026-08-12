# Planting Day (Foliage System v2)

> **GPU Indirect Drawing 기반의 대규모 식생 렌더링 최적화 및 절차적 자동 배치 툴 세트**  
> 기존 프리팹/브러시 배치 방식의 한계를 극복하고, 수십만 개의 식생을 모바일 환경에서도 고성능으로 렌더링·편집할 수 있도록 에디터 및 런타임 시스템을 전면 개편한 포스트모템 기반 개선 프로젝트입니다.

---

## 📖 시작하기 전에 — 사용 설명서를 먼저 읽어주세요

> ### ➡️ **[📕 기술_매뉴얼.pdf 열기](./기술_매뉴얼.pdf)**
>
> **툴 사용 전 반드시 정독**해야 하는 전체 사용 설명서입니다.
> Grass Builder의 DensityMap 베이킹 절차, Foliage Day 브러시 단축키, 팔레트 구성 및 데이터 저장 방식 등
> **아래 요약만으로는 알 수 없는 실제 작업 순서와 주의사항**이 모두 이 문서에 담겨 있습니다.
>
> ⚠️ 설명서의 절차를 건너뛰고 툴을 사용할 경우 **DensityMap 누락, 식생 데이터 손실** 등이 발생할 수 있습니다.

*아래 내용은 프로젝트 개요 요약이며, 설명서를 대체하지 않습니다.*

---

## 🛠 주요 개선 사항 (Key Changes)

### 1. 연산 및 에디터 최적화 (Raycast Optimization)
* **BVH (Bounding Volume Hierarchy) 공간 분할 도입**
  * 수천 개의 메시 삼각형을 매번 순회하던 기존 Raycast 문제 해결.
  * BVH 적용으로 200여 회의 Raycasting 연산 시간을 **10ms 이하**로 단축 (30 FPS 기준 안정적 동작 확보).
* **식생 데이터 전용 Quadtree 기법 적용**
  * 수십만 개 식생 탐색 시 프레임 저하를 방지하기 위해 위치 기반 Quadtree 적용.
  * **중간 삽입/삭제 알고리즘**을 추가하여 브러시 작업 중 지속적인 트리 재구성 오버헤드 최소화.

### 2. 런타임 렌더링 최적화 (GPU Indirect Drawing)
* **DrawMeshInstancedIndirect 기반 렌더링 파이프라인 구축**
  * CPU 병목 및 메모리 사용량을 최소화하여 수십만 개의 풀/나무를 GPU 버퍼 전달만으로 고속 렌더링.
* **ComputeShader 기반 커스텀 컬링 (FoliageDistanceCull)**
  * GPU 병렬 연산을 활용한 거리에 따른 식생 컬링 처리.
* **실험 결과 (Galaxy S22 Ultra 기준, 식생 30만 개 테스트)**
  * **FPS:** 기존 5 FPS 내외 → **25 FPS (플레이 가능 수준 확보)**
  * **메모리:** 기존 대비 **약 800MB 메모리 절감**
  * **Depth Priming 도입:** Cardboard 방식 반투명 식생의 Overdraw 이슈 대폭 개선.

### 3. 자연스러운 패턴 및 데이터 경량화
* **Poisson Disc Sampling & Blue Noise**
  * 기존 동심원/기본 랜덤 배치의 인위적 패턴을 지우고 자연스러운 균일 식생 밀도 구현.
* **Foliage Palette & Lightweight Structure**
  * Transform 정보 중심의 데이터 구조화(`PaletteSlotIdx`, `Position`, `RotationY`, `UniformScale`)로 저장 용량 최적화.
  * `WorldDensity` 파라미터를 통해 단일 데이터 세트 기반으로 옵션/플랫폼별 밀도 단계 가변 조정 지원.

### 4. 절차적 생성을 통한 자동 배치 (Procedural Generation)
* **SplatMap & SDF (Signed Distance Field) 기반 DensityMap 생성**
  * SplatMap 채널 정보를 샘플링하고 주변부 가중치 계산을 적용해 부드러운 경계 표현.
  * **SDF 마스킹:** 건물/나무 아래 영역 등의 식생을 자동 마스킹 및 마진 조절(Cutoff/Smooth)하여 리소스 낭비 방지.
  * **LOD Bias 일시 제어:** Top-View Orthographic 카메라 캡처 시 LOD 컬링에 의한 마스킹 누락 방지.

---

## 🔧 주요 툴 구성 (Toolset)

### 1. Grass Builder (절차적 자동 배치 툴)
* SplatMap, 레이어 기반 마스크(일반/SDF) 및 파라미터를 수집하여 **DensityMap** 자동 베이킹.
* 단 몇 분 내에 지형 규격에 맞춘 대규모 식생 기본 배치 완료.

### 2. Foliage Day (브러시 세부 편집 툴)
* **심기 (F2):** 반지름 및 간격 비율(밀도) 조절을 통한 실시간 식생 페인팅.
* **뽑기 (F3):** 지우기 강도를 조절하여 빽빽한 식생 영역의 부분/전체 삭제.
* **섞기:** 기존 배치된 식생과의 비율 조합 편집.
* **수집 모드 (Collect):** 기존 프리팹 기반으로 배치되어 있던 씬 데이터를 GPU 인스턴싱 타입으로 일괄 변환 및 통합.

---

## 📊 Performance Comparison

| 항목 | 기존 방식 (Prefab Instance) | 개선 방식 (GPU Indirect Drawing) |
| :--- | :--- | :--- |
| **30만 개 식생 프레임 (S22 Ultra)** | ~5 FPS (실행 불가 수준) | **~25 FPS (플레이 가능)** |
| **메모리 사용량** | Base | **-800 MB 절감** |
| **Raycasting (200회 기준)** | 수백 ms (지연 발생) | **< 10 ms (BVH 적용)** |
| **Overdraw 대응** | Overdraw 매우 높음 | **Depth Priming 적용으로 대폭 감소** |
